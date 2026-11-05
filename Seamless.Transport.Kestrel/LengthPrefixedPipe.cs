using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Seamless.Transport.Kestrel;

/// <summary>4バイト長のフレーム。解析中だけ入力を借用し、分割された大きい本文は消費しながら退避する。</summary>
public sealed class LengthPrefixedPipe{
    private readonly IDuplexPipe pipe;
    private readonly int maximum;
    private readonly bool bigEndian;
    public LengthPrefixedPipe(IDuplexPipe pipe,int maximum,bool bigEndian=false){ArgumentNullException.ThrowIfNull(pipe);ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);this.pipe=pipe;this.maximum=maximum;this.bigEndian=bigEndian;}
    public async ValueTask<T?> ReadAsync<T>(Func<ReadOnlySequence<byte>,T> parse,CancellationToken cancellationToken=default,Func<byte,int>? maximumPayload=null)where T:class{
        while(true){
            var result=await this.pipe.Input.ReadAsync(cancellationToken).ConfigureAwait(false);var buffer=result.Buffer;
            if(result.IsCanceled){this.pipe.Input.AdvanceTo(buffer.Start);throw new OperationCanceledException(cancellationToken);}
            if(buffer.Length<4){
                this.pipe.Input.AdvanceTo(buffer.Start,buffer.End);
                if(result.IsCompleted){if(buffer.IsEmpty){return null;}throw new EndOfStreamException("Incomplete frame header.");}continue;
            }
            int length;
            try{length=this.Length(buffer);if(length<1||length>this.maximum){throw new InvalidDataException("Invalid frame length.");}}
            catch{this.pipe.Input.AdvanceTo(buffer.Start);throw;}
            var body=buffer.Slice(4);
            if(maximumPayload is not null){
                if(body.IsEmpty){this.pipe.Input.AdvanceTo(buffer.Start,buffer.End);if(result.IsCompleted){throw new EndOfStreamException("Incomplete frame kind.");}continue;}
                try{var kind=new SequenceReader<byte>(body);kind.TryRead(out var value);if(length-1>maximumPayload(value)){throw new InvalidDataException("The frame exceeds the limit for its request kind.");}}
                catch{this.pipe.Input.AdvanceTo(buffer.Start);throw;}
            }
            if(body.Length>=length){var end=body.GetPosition(length);try{return parse(body.Slice(0,length));}finally{this.pipe.Input.AdvanceTo(end);}}
            // 保持したまま次のReadを待つと、本文がtransportのpause閾値を超えた時に停止する。
            var owned=ArrayPool<byte>.Shared.Rent(length);var copied=checked((int)body.Length);
            try{
                try{body.CopyTo(owned);}
                finally{this.pipe.Input.AdvanceTo(buffer.End);}
                if(result.IsCompleted){throw new EndOfStreamException("Incomplete frame payload.");}
                while(copied<length){
                    result=await this.pipe.Input.ReadAsync(cancellationToken).ConfigureAwait(false);buffer=result.Buffer;var count=(int)Math.Min(buffer.Length,length-copied);var end=buffer.GetPosition(count);
                    try{if(result.IsCanceled){throw new OperationCanceledException(cancellationToken);}buffer.Slice(0,count).CopyTo(owned.AsSpan(copied,count));copied+=count;}
                    finally{this.pipe.Input.AdvanceTo(end);}
                    if(result.IsCompleted&&copied<length){throw new EndOfStreamException("Incomplete frame payload.");}
                }
                return parse(new ReadOnlySequence<byte>(owned.AsMemory(0,length)));
            }finally{ArrayPool<byte>.Shared.Return(owned,clearArray:true);}
        }
    }
    private int Length(ReadOnlySequence<byte> sequence){Span<byte> prefix=stackalloc byte[4];sequence.Slice(0,4).CopyTo(prefix);if(this.bigEndian){return BinaryPrimitives.ReadInt32BigEndian(prefix);}return BinaryPrimitives.ReadInt32LittleEndian(prefix);}
    public async ValueTask WriteAsync(byte kind,ReadOnlyMemory<byte> payload,CancellationToken cancellationToken=default){
        if(payload.Length>this.maximum-1){throw new InvalidDataException("Frame exceeds the maximum size.");}
        var prefix=this.pipe.Output.GetSpan(5);if(this.bigEndian){BinaryPrimitives.WriteInt32BigEndian(prefix,payload.Length+1);}else{BinaryPrimitives.WriteInt32LittleEndian(prefix,payload.Length+1);}prefix[4]=kind;this.pipe.Output.Advance(5);
        var result=await this.pipe.Output.WriteAsync(payload,cancellationToken).ConfigureAwait(false);
        if(result.IsCanceled){throw new OperationCanceledException(cancellationToken);}if(result.IsCompleted){throw new IOException("The transport output has completed.");}
    }
}
