namespace Seamless.Transport.Kestrel;

/// <summary>接続の強制終了後も、開始済み処理のfinallyが終わるまで所有者を保持する。</summary>
internal sealed class ApplicationLifetime{
    private readonly object gate=new();
    private readonly TaskCompletionSource drained=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int active;
    private bool stopping;
    public bool TryEnter(){lock(this.gate){if(this.stopping){return false;}this.active++;return true;}}
    public void Exit(){lock(this.gate){this.active--;if(this.stopping&&this.active==0){this.drained.TrySetResult();}}}
    public Task Stop(){lock(this.gate){this.stopping=true;if(this.active==0){this.drained.TrySetResult();}return this.drained.Task;}}
}
