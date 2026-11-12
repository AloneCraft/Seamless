using System.Buffers.Binary;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Execution;

namespace Seamless.Database.Client.Results;

// 取得完了後に受信順で再生する。デコーダーは結果作成時に固定する。
internal sealed class ClientResultSpool<T>(ExecutionScope scope,long memoryThreshold,Func<ReadOnlySpan<byte>,T>? decode=null):IDisposable{
    private readonly Func<ReadOnlySpan<byte>,T> decode=decode??(static payload=>MemoryPackSerializer.Deserialize<T>(payload)!);
    private const int ChunkBytes=16<<10;
    private const int FileBufferBytes=64<<10;
    private readonly List<(OwnedBuffer Buffer,int Used)> chunks=[];
    private long retained;
    private TemporaryFile? file;
    private byte[]? fileBuffer;
    private int pendingBytes;
    private OwnedBuffer? replayBuffer;
    private int activeReplay;
    private ResourceReservation? fileMemory;
    private bool complete;
    private bool disposed;
    public bool Spilled=>this.file is not null;

    public void Append(ReadOnlySpan<byte> payload){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.complete){throw new InvalidOperationException("The result is complete.");}
        var length=checked(payload.Length+4);
        if(this.file is null&&(this.chunks.Count==0||this.chunks[^1].Buffer.Memory.Length-this.chunks[^1].Used<length)){
            var capacity=Math.Max(ChunkBytes,length);
            if(this.retained+capacity+64>memoryThreshold){
                this.MoveToFile();
            }else{
                this.chunks.Add((scope.Rent(capacity),0));
                this.retained+=capacity+64;
            }
        }
        if(this.file is not null){
            Span<byte> prefix=stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(prefix,payload.Length);
            this.AppendToFile(prefix);
            this.AppendToFile(payload);
        }else{
            var (buffer,used)=this.chunks[^1];
            var target=buffer.Memory.Span.Slice(used,length);
            BinaryPrimitives.WriteInt32LittleEndian(target,payload.Length);
            payload.CopyTo(target[4..]);
            this.chunks[^1]=(buffer,used+length);
        }
    }

    private void MoveToFile(){
        try{
            this.fileMemory=scope.Budget.ReserveMemory(FileBufferBytes+64);
            this.fileBuffer=new byte[FileBufferBytes];
        }catch(ResourceLimitException error)when(error.Kind==ResourceLimitKind.Memory&&this.chunks.Count!=0){
            // バッファと既存チャンクを同時に保持できない場合は、チャンクをそのまま書き出す。
        }
        this.file=scope.CreateTemporaryFile();
        foreach(var (buffer,used) in this.chunks){
            var bytes=buffer.Memory.Span[..used];
            if(this.fileBuffer is null){this.file.Write(bytes);}
            else{this.AppendToFile(bytes);}
            buffer.Dispose();
        }
        this.chunks.Clear();
        this.retained=0;
        if(this.fileBuffer is null){
            this.fileMemory=scope.Budget.ReserveMemory(FileBufferBytes+64);
            this.fileBuffer=new byte[FileBufferBytes];
        }
    }

    private void AppendToFile(ReadOnlySpan<byte> bytes){
        if(bytes.Length>FileBufferBytes-this.pendingBytes){this.FlushPendingWrites();}
        if(bytes.Length>=FileBufferBytes){
            this.file!.Write(bytes);
        }else{
            bytes.CopyTo(this.fileBuffer.AsSpan(this.pendingBytes));
            this.pendingBytes+=bytes.Length;
        }
    }

    private void FlushPendingWrites(){
        if(this.pendingBytes==0){return;}
        this.file!.Write(this.fileBuffer.AsSpan(0,this.pendingBytes));
        this.pendingBytes=0;
    }

    public void Complete(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.complete){throw new InvalidOperationException("The result is complete.");}
        if(this.file is not null){
            this.FlushPendingWrites();
            this.file.Flush();
        }
        this.complete=true;
    }

    public IEnumerable<T> ReadRows(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(!this.complete){throw new InvalidOperationException("Completed is required.");}
        // ファイルの読み取り位置と作業領域は、一つの列挙が終了または破棄まで所有する。
        if(Interlocked.CompareExchange(ref this.activeReplay,1,0)!=0){throw new InvalidOperationException("A result reader is already active.");}
        try{
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.file is null){
                foreach(var (buffer,used) in this.chunks){
                    for(var offset=0;offset<used;){
                        ObjectDisposedException.ThrowIf(this.disposed,this);
                        var length=BinaryPrimitives.ReadInt32LittleEndian(buffer.Memory.Span[offset..]);
                        offset+=4;
                        yield return this.decode(buffer.Memory.Span.Slice(offset,length));
                        offset+=length;
                    }
                }
            }else{
                var stream=this.file;
                var remaining=stream.Length;
                stream.Position=0;
                var offset=0;
                var available=0;
                while(remaining!=0){
                    ObjectDisposedException.ThrowIf(this.disposed,this);
                    this.FillReplay(4,ref offset,ref available);
                    var length=BinaryPrimitives.ReadInt32LittleEndian(this.fileBuffer.AsSpan(offset));
                    offset+=4;available-=4;
                    remaining-=4;
                    if(length<0||length>=NativeFrame.MaximumBytes||length>remaining){throw new InvalidDataException("Invalid stored frame length.");}
                    remaining-=length;
                    if(length<=FileBufferBytes){
                        this.FillReplay(length,ref offset,ref available);
                        var row=this.decode(this.fileBuffer.AsSpan(offset,length));
                        offset+=length;available-=length;
                        yield return row;
                    }else{
                        var buffer=this.replayBuffer;
                        if(buffer is null||buffer.Memory.Length<length){
                            this.ReleaseReplayBuffer();
                            this.replayBuffer=buffer=scope.Rent(length);
                        }
                        this.fileBuffer.AsSpan(offset,available).CopyTo(buffer.Memory.Span);
                        stream.ReadExactly(buffer.Memory.Span[available..length]);
                        offset=0;available=0;
                        yield return this.decode(buffer.Memory.Span[..length]);
                    }
                }
            }
        }finally{
            this.ReleaseReplayBuffer();
            Volatile.Write(ref this.activeReplay,0);
        }
    }

    private void ReleaseReplayBuffer(){
        this.replayBuffer?.Dispose();
        this.replayBuffer=null;
    }

    private void FillReplay(int required,ref int offset,ref int available){
        if(available>=required){return;}
        // 確定済みファイルは書込み用の予約と配列を再生にも使う。
        var buffer=this.fileBuffer.AsSpan();
        buffer.Slice(offset,available).CopyTo(buffer);
        offset=0;
        while(available<required){
            var read=this.file!.Read(buffer[available..]);
            if(read==0){throw new EndOfStreamException();}
            available+=read;
        }
    }

    public void Dispose(){
        if(this.disposed){return;}
        this.disposed=true;
        // 未完了の書込みバッファは破棄し、取消や受信エラーの後に書き込まない。
        this.fileBuffer=null;
        try{this.file?.Dispose();}
        finally{
            this.ReleaseReplayBuffer();
            this.fileMemory?.Dispose();
            foreach(var (buffer,_) in this.chunks){buffer.Dispose();}
            this.chunks.Clear();
        }
    }
}
