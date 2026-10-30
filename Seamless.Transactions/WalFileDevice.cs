using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using Seamless.Execution;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Seamless.Transactions;
/// <summary>区切りと起動世代を持つ追記専用WAL。検証後の修復以外で旧sectorを書き直さない。</summary>
public sealed class WalFileDevice:IWalDevice{
    private static readonly byte[] SegmentMagic=Encoding.ASCII.GetBytes("SLWAL002");
    private readonly string directory;
    private readonly WalOptions options;
    private readonly FileStream lease;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly ResourceReservation storage;
    private FileStream current=null!;
    private Guid walId;
    private long epoch;
    private long ordinal;
    private long lastLsn;
    private long firstSegment;
    private long baseLsn=1;
    private bool disposed;
    private readonly Dictionary<long,long> segmentEnds=[];
    public long RecoveredLsn{get;}
    public long FirstAvailableLsn=>Interlocked.Read(ref this.baseLsn);
    public Guid WalId=>this.walId;
    public string DirectoryPath=>this.directory;
    public WalFileDevice(string directory,WalOptions? options=null,ResourceBudget? budget=null){
        this.directory=Path.GetFullPath(directory);this.options=options??new();this.options.Validate();
        Directory.CreateDirectory(this.directory);
        this.lease=new FileStream(Path.Combine(this.directory,"wal.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        this.storage=(budget??new ResourceBudget(256L<<20,16L<<30)).ReserveTemporaryStorage(0);
        try{
            this.ValidateDeviceSector();
            this.RefreshStorage();
            var retention=this.ReadRetention();
            this.firstSegment=retention.FirstSegment;this.baseLsn=retention.BaseLsn;
            var files=this.Files();
            if(files.Length>0&&new FileInfo(files[^1]).Length<this.options.SectorBytes){
                // 新区切りのヘッダーは最初のフレームより先にSyncする。短い最終ヘッダーには応答済取引が存在しない。
                File.Delete(files[^1]);files=files[..^1];this.RefreshStorage();
            }
            var expected=this.baseLsn;var expectedOrdinal=this.firstSegment;long previousEpoch=0;
            foreach(var file in files){
                using var stream=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.Read);
                var header=this.ReadSegmentHeader(stream);
                if(header.Ordinal!=expectedOrdinal++||header.Base!=expected||header.Epoch<previousEpoch){throw new InvalidDataException("WAL segment/LSN/epoch discontinuity.");}
                previousEpoch=header.Epoch;this.epoch=Math.Max(this.epoch,header.Epoch);this.ordinal=header.Ordinal;
                if(this.walId==Guid.Empty){this.walId=header.Id;}
                if(this.walId!=header.Id||(retention.WalId!=Guid.Empty&&retention.WalId!=header.Id)){throw new InvalidDataException("WAL identity mismatch.");}
                var tail=stream.Position;
                foreach(var record in this.ReadFrames(stream,file==files[^1],expected)){
                    expected=record.Transaction.Lsn+1;tail=record.End;
                }
                // PADも含む最後の完全位置を再走査し、最後の未完suffixだけを修復する。
                tail=this.ValidEnd(stream,file==files[^1]);
                if(tail!=stream.Length){this.RepairTail(stream,tail);}
                stream.Flush(true);this.segmentEnds[header.Ordinal]=expected-1;
            }
            this.lastLsn=expected-1;this.RecoveredLsn=this.lastLsn;
            if(files.Length==0){
                if(this.firstSegment!=0||this.baseLsn!=1){throw new InvalidDataException("Retained WAL prefix is missing.");}
                this.walId=Guid.NewGuid();this.ordinal=-1;
            }
            foreach(var obsolete in Directory.GetFiles(this.directory,"segment-*.wal")){
                if(long.Parse(Path.GetFileNameWithoutExtension(obsolete).AsSpan(8),System.Globalization.CultureInfo.InvariantCulture)<this.firstSegment){File.Delete(obsolete);}
            }
            this.RefreshStorage();
            this.epoch=checked(this.epoch+1);this.NewSegment();
        }catch{this.current?.Dispose();this.lease.Dispose();this.storage.Dispose();throw;}
    }
    private void ValidateDeviceSector(){
        if(!OperatingSystem.IsWindows()){return;}
        if(!GetFileInformationByHandleEx(this.lease.SafeFileHandle,16,out var info,(uint)Marshal.SizeOf<StorageInfo>())){throw new IOException("Cannot establish WAL sector requirements.",new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));}
        var required=Math.Max(info.Logical,Math.Max(info.PhysicalAtomic,info.EffectiveAtomic));
        if(required==0||required>this.options.SectorBytes||info.SectorOffset!=0||info.PartitionOffset!=0){throw new NotSupportedException("Configure an aligned WAL SectorBytes at least as large as the device atomic sector: "+required);}
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int informationClass,out StorageInfo information,uint size);
    [StructLayout(LayoutKind.Sequential)]
    private struct StorageInfo{
        public uint Logical,PhysicalAtomic,PhysicalPerformance,EffectiveAtomic,Flags,SectorOffset,PartitionOffset;
    }
    public IEnumerable<WalTransaction> ReadTransactions(long maximumLsn=long.MaxValue){
        using(this.EnterDevice()){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            var expected=this.baseLsn;var ordinal=this.firstSegment;var files=this.Files();
            foreach(var file in files){
                if(expected>maximumLsn){yield break;}
                using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                var header=this.ReadSegmentHeader(stream);
                if(header.Id!=this.walId||header.Ordinal!=ordinal++||header.Base!=expected){throw new InvalidDataException("WAL segment identity/sequence mismatch.");}
                foreach(var record in this.ReadFrames(stream,file==files[^1],expected,maximumLsn)){
                    expected=checked(record.Transaction.Lsn+1);yield return record.Transaction;
                }
            }
        }
    }
    private string[] Files()=>Directory.GetFiles(this.directory,"segment-*.wal").Where(file=>{
        var name=Path.GetFileNameWithoutExtension(file);
        if(name.Length!=28||!long.TryParse(name.AsSpan(8),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var number)){throw new InvalidDataException("Invalid WAL segment name.");}
        // 確定済み保持台帳より古いファイルは、削除途中の停止で残っていても復旧対象に戻さない。
        return number>=this.firstSegment;
    }).Order(StringComparer.Ordinal).ToArray();
    private void RefreshStorage()=>this.storage.Resize(Directory.GetFiles(this.directory,"segment-*.wal").Sum(static path=>new FileInfo(path).Length));
    private void WriteCurrent(byte[] bytes){
        this.storage.Resize(checked(this.storage.Bytes+Math.Max(0,this.current.Position+bytes.Length-this.current.Length)));
        this.current.Write(bytes);
    }
    private string SegmentPath(long number)=>Path.Combine(this.directory,"segment-"+number.ToString("D20",System.Globalization.CultureInfo.InvariantCulture)+".wal");
    private void NewSegment(){
        this.ordinal=checked(this.ordinal+1);
        this.current=new FileStream(this.SegmentPath(this.ordinal),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read,4096,FileOptions.WriteThrough);
        var header=new byte[this.options.SectorBytes];var span=header.AsSpan();
        SegmentMagic.CopyTo(span);BinaryPrimitives.WriteInt32LittleEndian(span[8..],2);
        BinaryPrimitives.WriteInt32LittleEndian(span[12..],header.Length);this.walId.TryWriteBytes(span[16..32]);
        BinaryPrimitives.WriteInt64LittleEndian(span[32..],this.epoch);BinaryPrimitives.WriteInt64LittleEndian(span[40..],this.ordinal);
        BinaryPrimitives.WriteInt64LittleEndian(span[48..],checked(this.lastLsn+1));BinaryPrimitives.WriteInt32LittleEndian(span[56..],this.options.SectorBytes);
        BinaryPrimitives.WriteInt64LittleEndian(span[60..],this.options.SegmentBytes);
        SHA256.HashData(Encoding.UTF8.GetBytes(this.options.CatalogIdentity)).CopyTo(span[68..100]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[^4..],Crc32.HashToUInt32(span[..^4]));
        this.WriteCurrent(header);this.current.Flush(true);this.segmentEnds[this.ordinal]=this.lastLsn;
    }
    private (Guid Id,long Epoch,long Ordinal,long Base) ReadSegmentHeader(Stream stream){
        if(stream.Length>this.options.SegmentBytes){throw new InvalidDataException("WAL segment exceeds its configured bound.");}
        var header=new byte[this.options.SectorBytes];
        try{stream.ReadExactly(header);}catch(EndOfStreamException error){throw new InvalidDataException("Incomplete WAL segment header.",error);}
        var span=header.AsSpan();
        if(!span[..8].SequenceEqual(SegmentMagic)||BinaryPrimitives.ReadInt32LittleEndian(span[8..])!=2||BinaryPrimitives.ReadInt32LittleEndian(span[12..])!=header.Length||BinaryPrimitives.ReadInt32LittleEndian(span[56..])!=this.options.SectorBytes||BinaryPrimitives.ReadInt64LittleEndian(span[60..])!=this.options.SegmentBytes||!span[68..100].SequenceEqual(SHA256.HashData(Encoding.UTF8.GetBytes(this.options.CatalogIdentity)))||span[100..^4].IndexOfAnyExcept((byte)0)>=0||BinaryPrimitives.ReadUInt32LittleEndian(span[^4..])!=Crc32.HashToUInt32(span[..^4])){throw new InvalidDataException("WAL segment format/catalog/CRC mismatch.");}
        var result=(new Guid(span[16..32]),BinaryPrimitives.ReadInt64LittleEndian(span[32..]),BinaryPrimitives.ReadInt64LittleEndian(span[40..]),BinaryPrimitives.ReadInt64LittleEndian(span[48..]));
        if(result.Item1==Guid.Empty||result.Item2<=0||result.Item3<0||result.Item4<=0){throw new InvalidDataException("Invalid segment identity.");}
        return result;
    }
    private IEnumerable<(WalTransaction Transaction,long End)> ReadFrames(Stream stream,bool final,long expected,long maximumLsn=long.MaxValue){
        while(stream.Position<stream.Length){
            if(expected>maximumLsn){yield break;}
            var remaining=stream.Length-stream.Position;
            if(remaining<WalFormat.HeaderBytes){if(final){yield break;}throw new InvalidDataException("Incomplete nonfinal WAL header.");}
            var header=new byte[WalFormat.HeaderBytes];stream.ReadExactly(header);
            var info=WalFormat.ReadHeader(header,Math.Max(this.options.MaxFrameBytes,this.options.SectorBytes+WalFormat.HeaderBytes));
            if(info.Length>this.options.SegmentBytes-stream.Position+WalFormat.HeaderBytes){throw new InvalidDataException("WAL frame crosses segment boundary.");}
            if(info.Length-WalFormat.HeaderBytes>stream.Length-stream.Position){if(final){yield break;}throw new InvalidDataException("Incomplete nonfinal WAL body.");}
            var frame=new byte[info.Length];header.CopyTo(frame,0);stream.ReadExactly(frame.AsSpan(WalFormat.HeaderBytes));WalFormat.CheckBody(frame);
            if(info.Type==2&&(info.Length>this.options.SectorBytes+WalFormat.HeaderBytes-1||stream.Position%this.options.SectorBytes!=0)){throw new InvalidDataException("Invalid PAD sector boundary.");}
            if(info.Type==1){
                if(info.Lsn!=expected++){throw new InvalidDataException("WAL LSN gap/duplicate.");}
                yield return(WalFormat.Decode(frame,this.options.MaxFrameBytes),stream.Position);
            }
        }
    }
    private long ValidEnd(Stream stream,bool final){
        stream.Position=this.options.SectorBytes;var end=stream.Position;
        while(stream.Position<stream.Length){
            if(stream.Length-stream.Position<WalFormat.HeaderBytes){if(final){return end;}throw new InvalidDataException("Incomplete WAL header.");}
            var header=new byte[WalFormat.HeaderBytes];stream.ReadExactly(header);
            var info=WalFormat.ReadHeader(header,Math.Max(this.options.MaxFrameBytes,this.options.SectorBytes+WalFormat.HeaderBytes));
            if(info.Length-WalFormat.HeaderBytes>stream.Length-stream.Position){if(final){return end;}throw new InvalidDataException("Incomplete WAL body.");}
            stream.Position+=info.Length-WalFormat.HeaderBytes;end=stream.Position;
        }
        return end;
    }
    private void RepairTail(FileStream stream,long end){
        // 完全prefix以前を変更しない。最後のSyncはsector境界までPADを書いたため、未完suffixは応答済sectorにない。
        stream.Position=end;
        var remainder=(int)(end%this.options.SectorBytes);
        if(remainder!=0){
            var padding=this.options.SectorBytes-remainder;
            if(padding<WalFormat.HeaderBytes){padding+=this.options.SectorBytes;}
            var bytes=WalFormat.Pad(padding);
            this.storage.Resize(checked(this.storage.Bytes+Math.Max(0,stream.Position+bytes.Length-stream.Length)));
            stream.Write(bytes);
        }
        stream.SetLength(stream.Position);stream.Flush(true);
    }
    public void Write(byte[] frame){
        using(this.EnterDevice()){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            var transaction=WalFormat.Decode(frame,this.options.MaxFrameBytes);
            if(transaction.Lsn!=checked(this.lastLsn+1)){throw new InvalidDataException("Writer LSN discontinuity.");}
            if(this.current.Position+frame.Length+this.options.SectorBytes+WalFormat.HeaderBytes>this.options.SegmentBytes){
                this.SyncCore();this.current.Dispose();this.NewSegment();
            }
            this.WriteCurrent(frame);this.lastLsn=transaction.Lsn;this.segmentEnds[this.ordinal]=this.lastLsn;
        }
    }
    public void Sync(){
        using(this.EnterDevice()){this.SyncCore();}
    }
    private void SyncCore(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var remainder=(int)(this.current.Position%this.options.SectorBytes);
        if(remainder!=0){var count=this.options.SectorBytes-remainder;if(count<WalFormat.HeaderBytes){count+=this.options.SectorBytes;}this.WriteCurrent(WalFormat.Pad(count));}
        this.current.Flush(true);
    }
    /// <summary>全参加区画・結果台帳のcheckpointがcoverするprefixだけを、次の起動世代で削除する。</summary>
    public void PruneThrough(long checkpointLsn){
        using(this.EnterDevice()){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.SyncCore();
            var prior=this.ReadRetention();
            var candidate=this.firstSegment;
            foreach(var pair in this.segmentEnds.OrderBy(static p=>p.Key)){
                if(pair.Key>=this.ordinal||pair.Value>checkpointLsn){break;}candidate=pair.Key+1;
            }
            var removeBefore=this.firstSegment;
            if(prior.CandidateEpoch<this.epoch&&prior.CandidateEpoch>0){removeBefore=Math.Min(candidate,prior.CandidateSegment);}
            var nextBase=this.baseLsn;
            if(removeBefore>this.firstSegment){
                using var retained=File.OpenRead(this.SegmentPath(removeBefore));nextBase=this.ReadSegmentHeader(retained).Base;
            }
            var next=new Retention(this.walId,removeBefore,nextBase,this.epoch,candidate);
            var path=Path.Combine(this.directory,"retention.v2");var temporary=path+".new";
            var bytes=new byte[64];var span=bytes.AsSpan();Encoding.ASCII.GetBytes("SLRET002").CopyTo(span);next.WalId.TryWriteBytes(span[8..24]);
            BinaryPrimitives.WriteInt64LittleEndian(span[24..],next.FirstSegment);BinaryPrimitives.WriteInt64LittleEndian(span[32..],next.BaseLsn);
            BinaryPrimitives.WriteInt64LittleEndian(span[40..],next.CandidateEpoch);BinaryPrimitives.WriteInt64LittleEndian(span[48..],next.CandidateSegment);
            BinaryPrimitives.WriteUInt32LittleEndian(span[60..],Crc32.HashToUInt32(span[..60]));
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes);file.Flush(true);}
            File.Move(temporary,path,true);
            this.firstSegment=removeBefore;this.baseLsn=nextBase;
            foreach(var number in this.segmentEnds.Keys.Where(p=>p<removeBefore).ToArray()){File.Delete(this.SegmentPath(number));this.segmentEnds.Remove(number);}
            this.RefreshStorage();
        }
    }
    private Retention ReadRetention(){
        var path=Path.Combine(this.directory,"retention.v2");
        if(!File.Exists(path)){return new(Guid.Empty,0,1,0,0);}
        using var stream=File.OpenRead(path);if(stream.Length!=64){throw new InvalidDataException("Invalid WAL retention length.");}
        var bytes=new byte[64];stream.ReadExactly(bytes);var span=bytes.AsSpan();
        if(!span[..8].SequenceEqual(Encoding.ASCII.GetBytes("SLRET002"))||BinaryPrimitives.ReadInt32LittleEndian(span[56..])!=0||BinaryPrimitives.ReadUInt32LittleEndian(span[60..])!=Crc32.HashToUInt32(span[..60])){throw new InvalidDataException("WAL retention CRC/format mismatch.");}
        var value=new Retention(new Guid(span[8..24]),BinaryPrimitives.ReadInt64LittleEndian(span[24..]),BinaryPrimitives.ReadInt64LittleEndian(span[32..]),BinaryPrimitives.ReadInt64LittleEndian(span[40..]),BinaryPrimitives.ReadInt64LittleEndian(span[48..]));
        if(value.WalId==Guid.Empty||value.FirstSegment<0||value.BaseLsn<=0||value.CandidateEpoch<0||value.CandidateSegment<value.FirstSegment){throw new InvalidDataException("Invalid retention coverage.");}
        return value;
    }
    private IDisposable EnterDevice(){this.gate.Wait();return new DeviceLease(this.gate);}
    private sealed class DeviceLease(SemaphoreSlim gate):IDisposable{public void Dispose()=>gate.Release();}
    public void Dispose(){using(this.EnterDevice()){if(this.disposed){return;}this.disposed=true;try{this.current.Dispose();}finally{this.lease.Dispose();this.storage.Dispose();}}}
    private sealed record Retention(Guid WalId,long FirstSegment,long BaseLsn,long CandidateEpoch,long CandidateSegment);
}
