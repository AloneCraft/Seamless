namespace Seamless.Collections.Tsavorite;
/// <summary>
/// シングル用のコレクションが共有の読み取りセッションで走査している間を示す。Debug ビルドだけで、別のスレッドが同時に走査を始めたら
/// <see cref="InvalidOperationException"/> にする(BUG-0016。同時に走査すると、結果が誤ったり例外になったりする)。Release ビルドでは何もしない。
/// 同じスレッドの入れ子の走査は許す。
/// </summary>
internal readonly struct ScanScope:IDisposable {
#if DEBUG
    private readonly CollectionStore? store;
    internal ScanScope(CollectionStore store) {
        this.store=store;
        store.EnterScan();
    }
    public void Dispose() => this.store?.ExitScan();
#else
    public void Dispose() { }
#endif
}
