namespace Seamless.Transactions;
/// <summary>同期待ちとWrite/Sync障害を明示制御する試験装置。Sleepで順序を推測しない。</summary>
public sealed class FakeWalDevice:IWalDevice{
    private readonly ManualResetEventSlim syncGate=new(true);
    private readonly object gate=new();
    private readonly List<byte[]> frames=[];
    public ManualResetEventSlim SyncEntered{get;}=new(false);
    public Exception? WriteFailure{get;set;}
    public Exception? SyncFailure{get;set;}
    public int SyncCount{get;private set;}
    public IReadOnlyList<byte[]> Frames{get{lock(this.gate){return this.frames.Select(static f=>f.ToArray()).ToArray();}}}
    public void BlockSync(){this.SyncEntered.Reset();this.syncGate.Reset();}
    public void ReleaseSync()=>this.syncGate.Set();
    public void Write(byte[] frame){
        if(this.WriteFailure is not null){throw this.WriteFailure;}
        lock(this.gate){this.frames.Add(frame.ToArray());}
    }
    public void Sync(){
        this.SyncEntered.Set();this.syncGate.Wait();
        if(this.SyncFailure is not null){throw this.SyncFailure;}
        this.SyncCount++;
    }
    public void Dispose(){this.syncGate.Dispose();this.SyncEntered.Dispose();}
}
