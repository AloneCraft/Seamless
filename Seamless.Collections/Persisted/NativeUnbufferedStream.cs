using System.ComponentModel;
using Microsoft.Win32.SafeHandles;

namespace Seamless.Collections;

/// <summary>Windows の OS ファイルキャッシュを経由せず、整列した単一バッファで任意のバイト範囲を読み書きする。</summary>
internal sealed unsafe class NativeUnbufferedStream:Stream{
    private const uint GenericRead=0x80000000;
    private const uint GenericWrite=0x40000000;
    private const uint NoBuffering=0x20000000;
    private readonly object gate=new();
    private readonly FileOperations operations;
    private readonly SafeFileHandle handle;
    private readonly bool readable;
    private readonly bool writable;
    private readonly int alignment;
    private readonly int bufferLength;
    private readonly long appendStart;
    private byte* buffer;
    private long position;
    private bool disposed;

    public NativeUnbufferedStream(string path,FileMode mode,FileAccess access,FileShare share,StorageTuning tuning)
        :this(path,mode,access,share,tuning,new FileOperations()){
    }

    internal NativeUnbufferedStream(string path,FileMode mode,FileAccess access,FileShare share,StorageTuning tuning,FileOperations operations){
        ArgumentNullException.ThrowIfNull(operations);
        this.operations=operations;
        if(!OperatingSystem.IsWindows()){
            throw new PlatformNotSupportedException("Unbuffered Persisted access currently requires Windows.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if(access is not(FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite)){
            throw new ArgumentOutOfRangeException(nameof(access));
        }
        if((share&~(FileShare.Read|FileShare.Write|FileShare.Delete))!=0){
            throw new ArgumentOutOfRangeException(nameof(share));
        }
        this.readable=(access&FileAccess.Read)!=0;
        this.writable=(access&FileAccess.Write)!=0;
        if(!this.writable&&mode is FileMode.CreateNew or FileMode.Create or FileMode.Truncate or FileMode.Append){
            throw new ArgumentException("The file mode requires write access.",nameof(mode));
        }
        if(mode==FileMode.Append&&access!=FileAccess.Write){
            throw new ArgumentException("Append mode requires write-only access.",nameof(access));
        }
        uint disposition=mode switch{
            FileMode.CreateNew=>1,
            FileMode.Create=>2,
            FileMode.Open=>3,
            FileMode.OpenOrCreate=>4,
            FileMode.Truncate=>5,
            FileMode.Append=>4,
            _=>throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        // 部分セクターを書き戻すため、書き込み用ハンドルにも読み取り権限が必要。
        var desiredAccess=GenericRead;
        if(this.writable){
            desiredAccess|=GenericWrite;
        }
        var opened=CreateFileW(System.IO.Path.GetFullPath(path),desiredAccess,(uint)share,0,disposition,NoBuffering,0);
        if(opened.IsInvalid){
            var error=Marshal.GetLastWin32Error();
            this.operations.Close(opened);
            throw new IOException("Cannot open the unbuffered file.",new Win32Exception(error));
        }
        byte* allocated=null;
        try{
            if(!GetFileStorageInformation(opened,16,out var storage,(uint)sizeof(FileStorageInformation))){
                throw new IOException("Cannot query the file's storage alignment.",new Win32Exception(Marshal.GetLastWin32Error()));
            }
            if(!GetFileAlignmentInformation(opened,17,out var alignmentMask,sizeof(uint))){
                throw new IOException("Cannot query the file's required buffer alignment.",new Win32Exception(Marshal.GetLastWin32Error()));
            }
            var requiredAlignment=Math.Max(storage.LogicalBytesPerSector,Math.Max(storage.PhysicalBytesPerSectorForAtomicity,storage.PhysicalBytesPerSectorForPerformance));
            requiredAlignment=Math.Max(requiredAlignment,storage.FileSystemEffectivePhysicalBytesPerSectorForAtomicity);
            requiredAlignment=Math.Max(requiredAlignment,checked(alignmentMask+1));
            if(storage.LogicalBytesPerSector==0||requiredAlignment>int.MaxValue||(requiredAlignment&(requiredAlignment-1))!=0){
                throw new NotSupportedException("The file system returned an unsupported unbuffered I/O alignment.");
            }
            this.alignment=(int)requiredAlignment;
            var requestedLength=Math.Max(tuning.ResolveBufferSize(requiredAlignment),this.alignment);
            this.bufferLength=checked((int)(((long)requestedLength+this.alignment-1)/this.alignment*this.alignment));
            allocated=(byte*)NativeMemory.AlignedAlloc((nuint)this.bufferLength,(nuint)this.alignment);
            if(allocated is null){
                throw new OutOfMemoryException();
            }
            long appendOffset=0;
            if(mode==FileMode.Append){
                appendOffset=this.operations.GetLength(opened);
            }
            // 例外になり得る初期化を終えてから所有権を移す。失敗したコンストラクターの finalizer と二重解放しない。
            this.handle=opened;
            this.buffer=allocated;
            this.appendStart=appendOffset;
            this.position=appendOffset;
        }catch{
            try{
                if(allocated is not null){
                    this.operations.AlignedFree((nint)allocated);
                }
            }finally{
                this.operations.Close(opened);
            }
            throw;
        }
    }

    public override bool CanRead=>!this.disposed&&this.readable;
    public override bool CanSeek=>!this.disposed;
    public override bool CanWrite=>!this.disposed&&this.writable;
    public override long Length{
        get{
            lock(this.gate){
                this.ThrowIfDisposed();
                return this.operations.GetLength(this.handle);
            }
        }
    }
    public override long Position{
        get{
            lock(this.gate){
                this.ThrowIfDisposed();
                return this.position;
            }
        }
        set{
            lock(this.gate){
                this.ThrowIfDisposed();
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                this.ValidateAppendPosition(value);
                this.position=value;
            }
        }
    }

    public int ReadAt(long fileOffset,Span<byte> destination){
        lock(this.gate){
            this.ThrowIfDisposed();
            this.ThrowIfNotReadable();
            ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
            return this.ReadAtCore(fileOffset,destination);
        }
    }

    private int ReadAtCore(long fileOffset,Span<byte> destination){
        var length=this.operations.GetLength(this.handle);
        if(fileOffset>=length||destination.IsEmpty){
            return 0;
        }
        var remaining=(int)Math.Min(destination.Length,length-fileOffset);
        var total=0;
        while(remaining>0){
            var alignedOffset=fileOffset/this.alignment*this.alignment;
            var displacement=(int)(fileOffset-alignedOffset);
            var count=Math.Min(remaining,this.bufferLength-displacement);
            var ioLength=this.AlignLength(displacement+count);
            var alignedBuffer=new Span<byte>(this.buffer,ioLength);
            var read=this.operations.Read(this.handle,alignedBuffer,alignedOffset);
            var available=Math.Max(0,Math.Min(count,read-displacement));
            if(available==0){
                break;
            }
            alignedBuffer.Slice(displacement,available).CopyTo(destination.Slice(total,available));
            total+=available;
            remaining-=available;
            fileOffset+=available;
            if(available<count){
                break;
            }
        }
        return total;
    }

    public void WriteAt(long fileOffset,ReadOnlySpan<byte> source){
        lock(this.gate){
            this.ThrowIfDisposed();
            this.ThrowIfNotWritable();
            ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
            this.ValidateAppendPosition(fileOffset);
            this.WriteAtCore(fileOffset,source);
        }
    }

    private void WriteAtCore(long fileOffset,ReadOnlySpan<byte> source){
        if(source.IsEmpty){
            return;
        }
        var logicalLength=Math.Max(this.operations.GetLength(this.handle),checked(fileOffset+source.Length));
        while(!source.IsEmpty){
            var alignedOffset=fileOffset/this.alignment*this.alignment;
            var displacement=(int)(fileOffset-alignedOffset);
            var count=Math.Min(source.Length,this.bufferLength-displacement);
            var ioLength=this.AlignLength(displacement+count);
            var alignedBuffer=new Span<byte>(this.buffer,ioLength);
            if(displacement!=0||count!=ioLength){
                alignedBuffer.Clear();
                var existingLength=this.operations.GetLength(this.handle);
                if(alignedOffset<existingLength){
                    // EOF の余白だけゼロを許す。既存範囲を読めなかった場合は上書きしない。
                    var expectedRead=(int)Math.Min(ioLength,existingLength-alignedOffset);
                    var read=this.operations.Read(this.handle,alignedBuffer,alignedOffset);
                    if(read<expectedRead){
                        throw new IOException("Cannot preserve existing bytes after a short unbuffered read.");
                    }
                }
            }
            source[..count].CopyTo(alignedBuffer[displacement..]);
            var retainedLength=this.operations.GetLength(this.handle);
            try{
                this.operations.Write(this.handle,alignedBuffer,alignedOffset);
                retainedLength=logicalLength;
            }finally{
                if(checked(alignedOffset+ioLength)>retainedLength&&this.operations.GetLength(this.handle)>retainedLength){
                    // 失敗時はこの転送前の長さ、成功時は論理 EOF へ整列の余白を切り戻す。
                    this.operations.SetLength(this.handle,retainedLength);
                }
            }
            fileOffset+=count;
            source=source[count..];
        }
    }

    public override int Read(byte[] buffer,int offset,int count){
        ValidateBufferArguments(buffer,offset,count);
        return this.Read(buffer.AsSpan(offset,count));
    }
    public override int Read(Span<byte> destination){
        lock(this.gate){
            this.ThrowIfDisposed();
            this.ThrowIfNotReadable();
            var read=this.ReadAtCore(this.position,destination);
            this.position+=read;
            return read;
        }
    }
    public override void Write(byte[] buffer,int offset,int count){
        ValidateBufferArguments(buffer,offset,count);
        this.Write(buffer.AsSpan(offset,count));
    }
    public override void Write(ReadOnlySpan<byte> source){
        lock(this.gate){
            this.ThrowIfDisposed();
            this.ThrowIfNotWritable();
            this.ValidateAppendPosition(this.position);
            this.WriteAtCore(this.position,source);
            this.position=checked(this.position+source.Length);
        }
    }
    public override long Seek(long offset,SeekOrigin origin){
        lock(this.gate){
            this.ThrowIfDisposed();
            long position=origin switch{
                SeekOrigin.Begin=>offset,
                SeekOrigin.Current=>checked(this.position+offset),
                SeekOrigin.End=>checked(this.operations.GetLength(this.handle)+offset),
                _=>throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if(position<0){
                throw new IOException("Cannot seek before the beginning of the file.");
            }
            this.ValidateAppendPosition(position);
            this.position=position;
            return position;
        }
    }
    public override void SetLength(long value){
        lock(this.gate){
            this.ThrowIfDisposed();
            this.ThrowIfNotWritable();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            this.ValidateAppendPosition(value);
            this.operations.SetLength(this.handle,value);
            if(this.position>value){
                this.position=value;
            }
        }
    }
    public override void Flush()=>this.Flush(false);
    public void Flush(bool flushToDisk){
        lock(this.gate){
            this.ThrowIfDisposed();
            // Read 用ハンドルの FlushFileBuffers は拒否される。読み取り専用では書き出すものが無い。
            if(flushToDisk&&this.writable){
                this.operations.FlushToDisk(this.handle);
            }
        }
    }
    protected override void Dispose(bool disposing){
        lock(this.gate){
            if(!this.disposed){
                this.disposed=true;
                try{
                    if(this.handle is not null){
                        this.operations.Close(this.handle);
                    }
                }finally{
                    if(this.buffer is not null){
                        var ownedBuffer=this.buffer;
                        this.buffer=null;
                        this.operations.AlignedFree((nint)ownedBuffer);
                    }
                }
            }
        }
        base.Dispose(disposing);
    }
    ~NativeUnbufferedStream()=>this.Dispose(false);

    private int AlignLength(int length)=>checked((int)(((long)length+this.alignment-1)/this.alignment*this.alignment));
    private void ThrowIfDisposed()=>ObjectDisposedException.ThrowIf(this.disposed,this);
    private void ThrowIfNotReadable(){
        if(!this.readable){
            throw new NotSupportedException("The stream does not support reading.");
        }
    }
    private void ThrowIfNotWritable(){
        if(!this.writable){
            throw new NotSupportedException("The stream does not support writing.");
        }
    }
    private void ValidateAppendPosition(long value){
        if(value<this.appendStart){
            throw new IOException("Cannot access a position before the start of append mode.");
        }
    }

    /// <summary>各 stream が個別に持つ I/O 境界。既定動作を保ったまま、内部テストで失敗と資源解放を観測する。</summary>
    internal class FileOperations{
        public virtual int Read(SafeFileHandle handle,Span<byte> destination,long fileOffset)=>RandomAccess.Read(handle,destination,fileOffset);
        public virtual void Write(SafeFileHandle handle,ReadOnlySpan<byte> source,long fileOffset)=>RandomAccess.Write(handle,source,fileOffset);
        public virtual long GetLength(SafeFileHandle handle)=>RandomAccess.GetLength(handle);
        public virtual void SetLength(SafeFileHandle handle,long length)=>RandomAccess.SetLength(handle,length);
        public virtual void FlushToDisk(SafeFileHandle handle)=>RandomAccess.FlushToDisk(handle);
        public virtual void Close(SafeFileHandle handle)=>handle.Dispose();
        public virtual void AlignedFree(nint address)=>NativeMemory.AlignedFree((void*)address);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileStorageInformation{
        public uint LogicalBytesPerSector;
        public uint PhysicalBytesPerSectorForAtomicity;
        public uint PhysicalBytesPerSectorForPerformance;
        public uint FileSystemEffectivePhysicalBytesPerSectorForAtomicity;
        public uint Flags;
        public uint ByteOffsetForSectorAlignment;
        public uint ByteOffsetForPartitionAlignment;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFileW(string path,uint desiredAccess,uint shareMode,nint securityAttributes,uint creationDisposition,uint flagsAndAttributes,nint templateFile);
    [DllImport("kernel32.dll",EntryPoint="GetFileInformationByHandleEx",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileStorageInformation(SafeFileHandle handle,int informationClass,out FileStorageInformation information,uint bufferSize);
    [DllImport("kernel32.dll",EntryPoint="GetFileInformationByHandleEx",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileAlignmentInformation(SafeFileHandle handle,int informationClass,out uint information,uint bufferSize);
}
