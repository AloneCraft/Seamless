using System.Runtime.ExceptionServices;
namespace Benchmark.TpcC.Core;
/// <summary>runner専用の同期command worker。失敗後は次の取引を受け付けない。</summary>
internal sealed class TpcCDedicatedOwner:IDisposable{
    private readonly object gate=new();
    private readonly Thread thread;
    private Action? command;
    private ExceptionDispatchInfo? failure;
    private bool completed;
    private bool stopping;
    private bool faulted;
    public TpcCDedicatedOwner(){
        this.thread=new Thread(this.Run){Name="TPC-C transaction owner",IsBackground=true};
        this.thread.Start();
    }
    public int ThreadId=>this.thread.ManagedThreadId;
    public void Invoke(Action action){
        lock(this.gate){
            if(this.stopping||this.faulted||this.command is not null){
                throw new InvalidOperationException("停止・失敗・実行中のworkerは再利用できません。");
            }
            this.completed=false;
            this.command=action;
            Monitor.PulseAll(this.gate);
            while(!this.completed){
                Monitor.Wait(this.gate);
            }
            this.failure?.Throw();
        }
    }
    private void Run(){
        while(true){
            Action action;
            lock(this.gate){
                while(this.command is null&&!this.stopping){
                    Monitor.Wait(this.gate);
                }
                if(this.stopping){
                    return;
                }
                action=this.command!;
            }
            ExceptionDispatchInfo? error=null;
            try{
                action();
            }catch(Exception exception){
                error=ExceptionDispatchInfo.Capture(exception);
            }finally{
                lock(this.gate){
                    this.failure=error;
                    this.faulted=error is not null;
                    this.command=null;
                    this.completed=true;
                    Monitor.PulseAll(this.gate);
                }
            }
        }
    }
    public void Dispose(){
        lock(this.gate){
            this.stopping=true;
            Monitor.PulseAll(this.gate);
        }
        // InvokeのackにはBのcleanupまで含む。AのCloseAllより先に終端を確認する。
        this.thread.Join();
    }
}
