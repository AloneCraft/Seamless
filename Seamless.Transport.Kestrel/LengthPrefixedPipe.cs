using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Numerics;

namespace Seamless.Transport.Kestrel;

/// <summary>4バイト長のフレーム。解析中だけ入力を借用し、分割された大きい本文は消費しながら退避する。</summary>
public sealed class LengthPrefixedPipe{
    private readonly IDuplexPipe pipe;
    private readonly int maximum;
    private readonly bool bigEndian;
    private readonly TimeSpan? incompleteFrameTimeout;
    private readonly TimeSpan? writeTimeout;
    private readonly FrameBufferQuota? receiveBuffers;
    private readonly FrameBufferQuota? pendingWrites;
    private readonly MemoryPool<byte>? receivePool;
    private bool outputRejected;
    public bool OutputTimedOut{get;private set;}
    public bool OutputAborted=>this.OutputTimedOut||this.outputRejected;
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian=false):this(pipe,maximum,bigEndian,null){}
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian,TimeSpan? incompleteFrameTimeout):this(pipe,maximum,bigEndian,incompleteFrameTimeout,null){}
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian,TimeSpan? incompleteFrameTimeout,TimeSpan? writeTimeout):this(pipe,maximum,bigEndian,incompleteFrameTimeout,writeTimeout,null){}
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian,TimeSpan? incompleteFrameTimeout,TimeSpan? writeTimeout,FrameBufferQuota? receiveBuffers):this(pipe,maximum,bigEndian,incompleteFrameTimeout,writeTimeout,receiveBuffers,null){}
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian,TimeSpan? incompleteFrameTimeout,TimeSpan? writeTimeout,FrameBufferQuota? receiveBuffers,FrameBufferQuota? pendingWrites):this(pipe,maximum,bigEndian,incompleteFrameTimeout,writeTimeout,receiveBuffers,pendingWrites,null){}
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian,TimeSpan? incompleteFrameTimeout,TimeSpan? writeTimeout,FrameBufferQuota? receiveBuffers,FrameBufferQuota? pendingWrites,MemoryPool<byte>? receivePool){
        ArgumentNullException.ThrowIfNull(pipe);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        if(incompleteFrameTimeout is{} timeout&&(timeout<=TimeSpan.Zero||timeout.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(incompleteFrameTimeout));}
        if(writeTimeout is{} output&&(output<=TimeSpan.Zero||output.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(writeTimeout));}
        this.pipe=pipe;this.maximum=maximum;this.bigEndian=bigEndian;this.incompleteFrameTimeout=incompleteFrameTimeout;this.writeTimeout=writeTimeout;
        this.receiveBuffers=receiveBuffers;
        this.pendingWrites=pendingWrites;
        this.receivePool=receivePool;
    }
    public ValueTask<T?> ReadAsync<T>(Func<ReadOnlySequence<byte>,T> parse,CancellationToken cancellationToken=default,Func<byte,int>? maximumPayload=null)where T:class=>this.ReadAsync(parse,cancellationToken,maximumPayload,null);
    public async ValueTask<T?> ReadAsync<T>(Func<ReadOnlySequence<byte>,T> parse,CancellationToken cancellationToken,Func<byte,int>? maximumPayload,Action<int>? admit)where T:class{
        CancellationTokenSource? deadline=null;
        try{
        while(true){
            var result=await this.pipe.Input.ReadAsync(cancellationToken).ConfigureAwait(false);var buffer=result.Buffer;
            if(result.IsCanceled){this.pipe.Input.AdvanceTo(buffer.Start);throw new OperationCanceledException(cancellationToken);}
            if(buffer.Length<4){
                this.pipe.Input.AdvanceTo(buffer.Start,buffer.End);
                if(result.IsCompleted){if(buffer.IsEmpty){return null;}throw new EndOfStreamException("Incomplete frame header.");}
                if(!buffer.IsEmpty){StartDeadline();}
                continue;
            }
            int length;
            try{length=this.Length(buffer);if(length<1||length>this.maximum){throw new InvalidDataException("Invalid frame length.");}}
            catch{this.pipe.Input.AdvanceTo(buffer.Start);throw;}
            var body=buffer.Slice(4);
            if(maximumPayload is not null){
                if(body.IsEmpty){this.pipe.Input.AdvanceTo(buffer.Start,buffer.End);if(result.IsCompleted){throw new EndOfStreamException("Incomplete frame kind.");}StartDeadline();continue;}
                try{var kind=new SequenceReader<byte>(body);kind.TryRead(out var value);if(length-1>maximumPayload(value)){throw new InvalidDataException("The frame exceeds the limit for its request kind.");}}
                catch{this.pipe.Input.AdvanceTo(buffer.Start);throw;}
            }
            try{admit?.Invoke(length);}
            catch{this.pipe.Input.AdvanceTo(buffer.Start);throw;}
            if(body.Length>=length){var end=body.GetPosition(length);try{return parse(body.Slice(0,length));}finally{this.pipe.Input.AdvanceTo(end);}}
            // 保持したまま次のReadを待つと、本文がtransportのpause閾値を超えた時に停止する。
            byte[]? owned=null;
            IMemoryOwner<byte>? rented=null;Memory<byte> memory=default;
            long reserved=0;
            var copied=checked((int)body.Length);
            try{
                try{
                    if(result.IsCompleted){throw new EndOfStreamException("Incomplete frame payload.");}
                    StartDeadline();
                    if(this.receiveBuffers is{} quota){
                        // 共有プールのバケット容量を、配列を借りる前に確保する。
                        var capacity=(long)BitOperations.RoundUpToPowerOf2((ulong)Math.Max(length,16));
                        if(!quota.TryAcquire(capacity)){throw new IOException("The frame receive buffer limit has been reached.");}
                        reserved=capacity;
                    }
                    if(this.receivePool is{} pool){rented=pool.Rent(length);memory=rented.Memory;}
                    else{owned=ArrayPool<byte>.Shared.Rent(length);memory=owned;}
                    if(memory.Length<length||reserved!=0&&memory.Length>reserved){throw new IOException("The frame buffer exceeded its reserved capacity.");}
                    body.CopyTo(memory.Span);
                }
                finally{this.pipe.Input.AdvanceTo(buffer.End);}
                while(copied<length){
                    result=await this.pipe.Input.ReadAsync(cancellationToken).ConfigureAwait(false);buffer=result.Buffer;var count=(int)Math.Min(buffer.Length,length-copied);var end=buffer.GetPosition(count);
                    try{if(result.IsCanceled){throw new OperationCanceledException(cancellationToken);}buffer.Slice(0,count).CopyTo(memory.Span.Slice(copied,count));copied+=count;}
                    finally{this.pipe.Input.AdvanceTo(end);}
                    if(result.IsCompleted&&copied<length){throw new EndOfStreamException("Incomplete frame payload.");}
                }
                return parse(new ReadOnlySequence<byte>(memory.Slice(0,length)));
            }finally{
                try{
                    if(rented is not null){try{rented.Memory.Span.Clear();}finally{rented.Dispose();}}
                    else if(owned is not null){ArrayPool<byte>.Shared.Return(owned,clearArray:true);}
                }
                finally{if(reserved!=0){this.receiveBuffers!.Release(reserved);}}
            }
        }
        }finally{deadline?.Dispose();}

        void StartDeadline(){
            if(deadline is not null||this.incompleteFrameTimeout is not{} timeout){return;}
            deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            cancellationToken=deadline.Token;
        }
    }
    private int Length(ReadOnlySequence<byte> sequence){Span<byte> prefix=stackalloc byte[4];sequence.Slice(0,4).CopyTo(prefix);if(this.bigEndian){return BinaryPrimitives.ReadInt32BigEndian(prefix);}return BinaryPrimitives.ReadInt32LittleEndian(prefix);}
    public async ValueTask WriteAsync(byte kind,ReadOnlyMemory<byte> payload,CancellationToken cancellationToken=default){
        if(this.OutputTimedOut){throw new IOException("The transport output has timed out.");}
        if(this.outputRejected){throw new IOException("The pending write limit has been reached.");}
        if(payload.Length>this.maximum-1){throw new InvalidDataException("Frame exceeds the maximum size.");}
        using var reservation=this.pendingWrites?.TryReserve(5L+payload.Length);
        if(this.pendingWrites is not null&&reservation is null){
            this.outputRejected=true;
            throw new IOException("The pending write limit has been reached.");
        }
        using var deadline=this.writeTimeout.HasValue?CancellationTokenSource.CreateLinkedTokenSource(cancellationToken):null;
        if(this.writeTimeout is{} timeout){deadline!.CancelAfter(timeout);}
        var prefix=this.pipe.Output.GetSpan(5);if(this.bigEndian){BinaryPrimitives.WriteInt32BigEndian(prefix,payload.Length+1);}else{BinaryPrimitives.WriteInt32LittleEndian(prefix,payload.Length+1);}prefix[4]=kind;this.pipe.Output.Advance(5);
        try{
            var result=await this.pipe.Output.WriteAsync(payload,deadline?.Token??cancellationToken).ConfigureAwait(false);
            if(result.IsCanceled){throw new OperationCanceledException(deadline?.Token??cancellationToken);}
            if(result.IsCompleted){throw new IOException("The transport output has completed.");}
        }catch(OperationCanceledException error)when(deadline?.IsCancellationRequested==true&&!cancellationToken.IsCancellationRequested){
            this.OutputTimedOut=true;
            throw new IOException("The transport output has timed out.",error);
        }
    }
}
