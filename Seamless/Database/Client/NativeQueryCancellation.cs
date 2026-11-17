using System.Buffers.Binary;

namespace Seamless.Database.Client;

/// <summary>同じ要求の終端まで読み終えてから、接続の実行枠を返す。</summary>
internal sealed class NativeQueryCancellation:IAsyncDisposable{
    private readonly object gate=new();
    private readonly Stream stream;
    private readonly long sequence;
    private readonly CancellationTokenSource deadline=new();
    private readonly CancellationTokenRegistration registration;
    private Task? sent;
    private bool listening=true;
    internal bool Acknowledged{get;private set;}
    internal CancellationToken ReadToken=>this.deadline.Token;
    internal bool Requested{get{lock(this.gate){return this.sent is not null;}}}
    internal NativeQueryCancellation(Stream stream,long sequence,CancellationToken cancellationToken){
        this.stream=stream;this.sequence=sequence;
        this.registration=cancellationToken.Register(static state=>((NativeQueryCancellation)state!).Request(),this);
    }
    private void Request(){lock(this.gate){if(this.listening){this.sent??=this.SendAsync();}}}
    private async Task SendAsync(){
        this.deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var payload=new byte[8];BinaryPrimitives.WriteInt64LittleEndian(payload,this.sequence);
        await NativeFrame.WriteAsync(this.stream,NativeFrame.Cancel,payload,this.deadline.Token).ConfigureAwait(false);
    }
    internal void StopListening(){
        this.registration.Dispose();lock(this.gate){this.listening=false;}
    }
    internal void Acknowledge(ReadOnlySpan<byte> payload){
        if(payload.Length!=8||BinaryPrimitives.ReadInt64LittleEndian(payload)!=this.sequence||!this.Requested){throw new InvalidDataException("The native cancellation acknowledgement does not match the request.");}
        this.Acknowledged=true;
    }
    internal async Task<bool> FinishAsync(){
        try{
            Task send;lock(this.gate){send=this.sent??=this.SendAsync();}
            await send.ConfigureAwait(false);
            if(this.Acknowledged){return true;}
            var header=new byte[5];var discard=new byte[4096];
            while(true){
                var (kind,length)=await NativeFrame.ReadHeaderAsync(this.stream,header,this.deadline.Token).ConfigureAwait(false)??throw new EndOfStreamException();
                if(kind==NativeFrame.Cancelled){
                    if(length!=8){throw new InvalidDataException("Invalid native cancellation acknowledgement.");}
                    await this.stream.ReadExactlyAsync(discard.AsMemory(0,8),this.deadline.Token).ConfigureAwait(false);this.Acknowledge(discard.AsSpan(0,8));return true;
                }
                if(kind is not(NativeFrame.Row or NativeFrame.Completed or NativeFrame.Error or NativeFrame.RecoverableError)||kind==NativeFrame.Completed&&length!=0){throw new InvalidDataException("Unexpected frame while cancelling a native request.");}
                while(length>0){var count=Math.Min(length,discard.Length);await this.stream.ReadExactlyAsync(discard.AsMemory(0,count),this.deadline.Token).ConfigureAwait(false);length-=count;}
            }
        }catch(Exception error)when(error is IOException or OperationCanceledException or InvalidDataException or ObjectDisposedException){return false;}
    }
    public async ValueTask DisposeAsync(){
        this.StopListening();
        try{
            this.deadline.Cancel();Task? pending;lock(this.gate){pending=this.sent;}
            if(pending is not null){
                try{await pending.ConfigureAwait(false);}
                catch(Exception error)when(error is IOException or OperationCanceledException or InvalidDataException or ObjectDisposedException){}
            }
        }finally{this.deadline.Dispose();}
    }
}
