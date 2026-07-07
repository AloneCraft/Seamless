using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed class UiRenderScheduler{
    private readonly IUiDispatcher dispatcher;
    private readonly RunEventPipeline pipeline;
    private readonly UiDiagnostics diagnostics;
    private readonly Action<UiSnapshot> apply;
    private int scheduled;

    public UiRenderScheduler(
        IUiDispatcher dispatcher,
        RunEventPipeline pipeline,
        UiDiagnostics diagnostics,
        Action<UiSnapshot> apply){
        this.dispatcher=dispatcher;
        this.pipeline=pipeline;
        this.diagnostics=diagnostics;
        this.apply=apply;
    }

    public void RequestRender(){
        if(Interlocked.Exchange(ref this.scheduled,1)==1){
            return;
        }

        _=Task.Run(async () => {
            await Task.Delay(PipelineConstants.UiRenderIntervalMs);
            this.dispatcher.BeginInvoke(this.RenderOnce);
        });
    }

    public void FlushNow(){
        this.dispatcher.BeginInvoke(this.RenderOnce);
    }

    private void RenderOnce(){
        Interlocked.Exchange(ref this.scheduled,0);
        var metrics = this.diagnostics.CreateMetrics(0,this.pipeline.PendingCount,0);
        if(this.pipeline.TryCreateUiSnapshot(metrics,out var snapshot)){
            var stopwatch = Stopwatch.StartNew();
            this.apply(snapshot);
            stopwatch.Stop();
            this.diagnostics.RecordRender(
                stopwatch.Elapsed.TotalMilliseconds,
                snapshot.Metrics,
                snapshot.RunningUpdates.Count+snapshot.TerminalResults.Count);
            UiThreadBudget.Record("render-total",stopwatch.Elapsed.TotalMilliseconds);
        }

        if(this.pipeline.HasPending){
            this.RequestRender();
        }
    }
}
