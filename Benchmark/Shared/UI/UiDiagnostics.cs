using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed class UiDiagnostics{
    private readonly Action<string,Exception?> log;
    private long receivedEvents;
    private long renderedEvents;
    private long dispatcherStallCount;
    private double lastDispatcherGapMilliseconds;
    private double lastRenderMilliseconds;
    private double lastPersistenceMilliseconds;
    private int runningUpdateCount;
    private int terminalBacklogCount;
    private int pendingPersistenceEvents;
    private int activeRunningCount;
    private int dirtyRunningCount;
    private int terminalAppliedCount;
    private long coalescedRunningEvents;
    private long consumedEvents;
    private CancellationTokenSource? heartbeatCancellation;

    public UiDiagnostics(Action<string,Exception?> log){
        this.log=log;
    }

    public void StartHeartbeat(IUiDispatcher dispatcher){
        if(this.heartbeatCancellation is not null){
            return;
        }

        var source = new CancellationTokenSource();
        this.heartbeatCancellation=source;
        _=Task.Run(async () => {
            var expectedAt = DateTimeOffset.UtcNow+TimeSpan.FromMilliseconds(PipelineConstants.UiHeartbeatIntervalMs);
            var lastBudgetReportAt = DateTimeOffset.UtcNow;
            while(!source.IsCancellationRequested){
                try{
                    await Task.Delay(PipelineConstants.UiHeartbeatIntervalMs,source.Token);
                }
                catch(OperationCanceledException){
                    break;
                }

                var reportNow = DateTimeOffset.UtcNow;
                if(reportNow-lastBudgetReportAt>=TimeSpan.FromSeconds(5)){
                    lastBudgetReportAt=reportNow;
                    var report = UiThreadBudget.DrainReport();
                    if(!string.IsNullOrEmpty(report)){
                        Trace.WriteLine("[UI][budget] "+report);
                        // Trace出力だけではMAUIアプリ実行時にユーザーが確認できなぁE��め、startup.logにも残す、E
                        this.log("[UI][budget] "+report,null);
                    }
                }

                dispatcher.BeginInvoke(() => {
                    var now = DateTimeOffset.UtcNow;
                    var gap = (now-expectedAt).TotalMilliseconds;
                    expectedAt=now+TimeSpan.FromMilliseconds(PipelineConstants.UiHeartbeatIntervalMs);
                    if(gap>PipelineConstants.UiResponsivenessTargetMs){
                        Interlocked.Increment(ref this.dispatcherStallCount);
                        this.lastDispatcherGapMilliseconds=gap;
                        this.log("Benchmark UI dispatcher stall: "+gap.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms",null);
                        Trace.WriteLine($"[UI][heartbeat] STALL gap={gap:0.0}ms");
                    }
                });
            }
        });
    }

    public void RecordReceivedEvent(string status){
        Interlocked.Increment(ref this.receivedEvents);
    }

    public void RecordRender(double elapsedMilliseconds,PipelineMetrics metrics,int renderedCount){
        this.lastRenderMilliseconds=elapsedMilliseconds;
        this.runningUpdateCount=metrics.RunningUpdateCount;
        this.terminalBacklogCount=metrics.TerminalBacklogCount;
        this.activeRunningCount=metrics.ActiveRunningCount;
        this.dirtyRunningCount=metrics.DirtyRunningCount;
        this.terminalAppliedCount=metrics.TerminalAppliedCount;
        this.coalescedRunningEvents=metrics.CoalescedRunningEvents;
        this.consumedEvents=metrics.ConsumedEvents;
        Interlocked.Add(ref this.renderedEvents,renderedCount);
    }

    public void RecordPersistence(double elapsedMilliseconds,int pendingPersistenceEvents){
        this.lastPersistenceMilliseconds=elapsedMilliseconds;
        this.pendingPersistenceEvents=pendingPersistenceEvents;
    }

    public PipelineMetrics CreateMetrics(int runningUpdateCount,int terminalBacklogCount,int pendingPersistenceEvents) =>
        new(
            Interlocked.Read(ref this.receivedEvents),
            this.consumedEvents,
            Interlocked.Read(ref this.renderedEvents),
            this.coalescedRunningEvents,
            runningUpdateCount,
            terminalBacklogCount,
            pendingPersistenceEvents,
            this.lastRenderMilliseconds,
            this.lastPersistenceMilliseconds,
            this.lastDispatcherGapMilliseconds,
            Interlocked.Read(ref this.dispatcherStallCount),
            this.activeRunningCount,
            this.dirtyRunningCount,
            this.terminalAppliedCount);

    public IReadOnlyList<DiagnosticRow> CreateRows(){
        var metrics = this.CreateMetrics(this.runningUpdateCount,this.terminalBacklogCount,this.pendingPersistenceEvents);
        return [
            new DiagnosticRow("UI","dispatcher gap","target",PipelineConstants.UiResponsivenessTargetMs.ToString(System.Globalization.CultureInfo.InvariantCulture)+"ms",metrics.LastDispatcherGapMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms",metrics.DispatcherStallCount.ToString(System.Globalization.CultureInfo.InvariantCulture),"Last dispatcher delay over heartbeat target."),
            new DiagnosticRow("UI","render batch","running",metrics.RunningUpdateCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.LastRenderMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms",metrics.RenderedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),"Non-blocking 100ms snapshot render."),
            new DiagnosticRow("UI","dirty running","active",metrics.DirtyRunningCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.ActiveRunningCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.TerminalAppliedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),"Dirty running, active running, and terminal items applied in last snapshot."),
            new DiagnosticRow("Event","coalesced running","count",metrics.CoalescedRunningEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.ReceivedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.ConsumedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),"Old Running updates dropped by key."),
            new DiagnosticRow("Event","terminal backlog","count",metrics.TerminalBacklogCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.PendingPersistenceEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.LastPersistenceMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms","Completed/Failed backlog and SQLite batch.")
        ];
    }
}
