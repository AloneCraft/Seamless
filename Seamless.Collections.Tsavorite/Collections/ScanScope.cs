namespace Seamless.Collections.Tsavorite;
/// <summary>シングル用ハンドルで共有する読み取りセッションへの同期アクセスを保護する。</summary>
internal readonly struct ScanScope:IDisposable{
    private readonly object? gate;
#if DEBUG
    private readonly CollectionStore? store;
#endif
    internal ScanScope(object gate,CollectionStore? store){
        global::System.Threading.Monitor.Enter(gate);
        this.gate=gate;
#if DEBUG
        this.store=store;
        try{this.store?.EnterScan();}
        catch{global::System.Threading.Monitor.Exit(gate);throw;}
#endif
    }
    public void Dispose(){
        if(this.gate is null){return;}
        try{
#if DEBUG
            this.store?.ExitScan();
#endif
        }finally{global::System.Threading.Monitor.Exit(this.gate);}
    }
}
