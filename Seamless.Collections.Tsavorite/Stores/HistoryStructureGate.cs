namespace Seamless.Collections.Tsavorite;

/// <summary>遅延列挙のMoveNext間でスレッドが変わっても返却できる構造保護。</summary>
internal sealed class HistoryStructureGate{
    private readonly object sync=new();
    private int readers;
    private bool writing;
    internal IDisposable Read(){lock(this.sync){while(this.writing){Monitor.Wait(this.sync);}this.readers++;return new Lease(this,false);}}
    internal IDisposable Write(){lock(this.sync){while(this.writing||this.readers!=0){Monitor.Wait(this.sync);}this.writing=true;return new Lease(this,true);}}
    private void Release(bool write){lock(this.sync){if(write){this.writing=false;}else{this.readers--;}Monitor.PulseAll(this.sync);}}
    private sealed class Lease(HistoryStructureGate owner,bool write):IDisposable{
        private HistoryStructureGate? owner=owner;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release(write);
    }
}
