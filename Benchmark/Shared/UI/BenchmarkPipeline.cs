using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;

internal static class BenchmarkPipelineConstants{
    public const int UiResponsivenessTargetMs = 100;
    public const int UiHeartbeatIntervalMs = 50;
    public const int UiRenderIntervalMs = 100;
    public const int ProgressRefreshIntervalMs = 500;
    public const int ChartRefreshIntervalMs = 1000;
    public const int PersistenceBatchIntervalMs = 1000;
    public const int MaxTerminalResultsPerTick = 6;
}

/// <summary>
/// UIスレッドで実行された各区間の所要時間を集計し、5秒毎に1行のTraceレポートに出す。
/// どの区間がUIスレッド時間を支配しているかを、コンソールスパム無しで実測特定するための計器。
/// </summary>
internal static class UiThreadBudget{
    private sealed class Entry{
        public long Count;
        public double TotalMs;
        public double MaxMs;
    }

    private static readonly ConcurrentDictionary<string,Entry> entries = new(StringComparer.Ordinal);

    public static void Record(string name,double elapsedMs){
        var entry = entries.GetOrAdd(name,static _ => new Entry());
        lock(entry){
            entry.Count++;
            entry.TotalMs+=elapsedMs;
            if(elapsedMs>entry.MaxMs){
                entry.MaxMs=elapsedMs;
            }
        }
    }

    public static void Measure(string name,Action action){
        var start = Stopwatch.GetTimestamp();
        try{
            action();
        }finally{
            Record(name,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    public static string DrainReport(){
        var items = entries.ToArray();
        entries.Clear();
        if(items.Length==0){
            return string.Empty;
        }

        return string.Join(" | ",items
            .OrderByDescending(static item => item.Value.TotalMs)
            .Take(8)
            .Select(static item => $"{item.Key}={item.Value.TotalMs:0}ms x{item.Value.Count} max={item.Value.MaxMs:0}"));
    }
}

internal sealed record BenchmarkRunEvent(
    BenchmarkResultItem Result,
    int RunGeneration,
    long? UiRunId,
    DateTimeOffset ReceivedAt);

internal sealed record BenchmarkPersistenceItem(
    BenchmarkResultItem Result,
    int RunGeneration,
    long? UiRunId);

internal sealed record BenchmarkUiSnapshot(
    IReadOnlyList<BenchmarkRunEvent> RunningUpdates,
    IReadOnlyList<BenchmarkRunEvent> TerminalResults,
    bool RefreshProgress,
    bool RefreshChart,
    BenchmarkPipelineMetrics Metrics){
    public bool HasUpdates => this.RunningUpdates.Count>0||this.TerminalResults.Count>0||this.RefreshProgress||this.RefreshChart;
}

internal sealed record BenchmarkPipelineMetrics(
    long ReceivedEvents,
    long ConsumedEvents,
    long RenderedEvents,
    long CoalescedRunningEvents,
    int RunningUpdateCount,
    int TerminalBacklogCount,
    int PendingPersistenceEvents,
    double LastRenderMilliseconds,
    double LastPersistenceMilliseconds,
    double LastDispatcherGapMilliseconds,
    long DispatcherStallCount,
    int ActiveRunningCount,
    int DirtyRunningCount,
    int TerminalAppliedCount);

internal sealed class BenchmarkRunEventPipeline{
    private readonly Channel<BenchmarkRunEvent> channel = Channel.CreateUnbounded<BenchmarkRunEvent>(new UnboundedChannelOptions{
        SingleReader=true,
        SingleWriter=false,
        AllowSynchronousContinuations=false,
    });
    private readonly object stateGate = new();
    private readonly Dictionary<string,BenchmarkRunEvent> runningByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> dirtyRunningKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BenchmarkRunEvent> terminalPrecedingRunningUpdates = [];
    private readonly Queue<BenchmarkRunEvent> terminalResults = new();
    private long receivedEvents;
    private long consumedEvents;
    private long renderedEvents;
    private long coalescedRunningEvents;
    private bool chartDirty;
    private DateTimeOffset lastProgressSnapshotAt = DateTimeOffset.MinValue;
    private DateTimeOffset lastChartSnapshotAt = DateTimeOffset.MinValue;

    public BenchmarkRunEventPipeline(){
        _=Task.Run(this.ConsumeAsync);
    }

    public void Post(BenchmarkRunEvent item){
        Interlocked.Increment(ref this.receivedEvents);
        this.channel.Writer.TryWrite(item);
    }

    public void Clear(){
        lock(this.stateGate){
            this.runningByKey.Clear();
            this.dirtyRunningKeys.Clear();
            this.terminalPrecedingRunningUpdates.Clear();
            this.terminalResults.Clear();
            this.chartDirty=false;
        }
    }

    public bool TryCreateUiSnapshot(BenchmarkPipelineMetrics metrics,out BenchmarkUiSnapshot snapshot){
        var now = DateTimeOffset.UtcNow;
        lock(this.stateGate){
            var running = new List<BenchmarkRunEvent>(this.dirtyRunningKeys.Count+this.terminalPrecedingRunningUpdates.Count);
            running.AddRange(this.terminalPrecedingRunningUpdates);
            this.terminalPrecedingRunningUpdates.Clear();
            foreach(var key in this.dirtyRunningKeys){
                if(this.runningByKey.TryGetValue(key,out var item)){
                    running.Add(item);
                }
            }
            this.dirtyRunningKeys.Clear();
            var terminal = new List<BenchmarkRunEvent>(BenchmarkPipelineConstants.MaxTerminalResultsPerTick);
            while(terminal.Count<BenchmarkPipelineConstants.MaxTerminalResultsPerTick&&this.terminalResults.Count>0){
                terminal.Add(this.terminalResults.Dequeue());
            }

            var refreshProgress = false;
            if((running.Count>0||terminal.Count>0)&&now-this.lastProgressSnapshotAt>=TimeSpan.FromMilliseconds(BenchmarkPipelineConstants.ProgressRefreshIntervalMs)){
                refreshProgress=true;
                this.lastProgressSnapshotAt=now;
            }

            var refreshChart = false;
            if(this.chartDirty&&now-this.lastChartSnapshotAt>=TimeSpan.FromMilliseconds(BenchmarkPipelineConstants.ChartRefreshIntervalMs)){
                refreshChart=true;
                this.chartDirty=false;
                this.lastChartSnapshotAt=now;
            }

            var currentMetrics = metrics with{
                ReceivedEvents=Interlocked.Read(ref this.receivedEvents),
                ConsumedEvents=Interlocked.Read(ref this.consumedEvents),
                RenderedEvents=Interlocked.Read(ref this.renderedEvents),
                CoalescedRunningEvents=Interlocked.Read(ref this.coalescedRunningEvents),
                RunningUpdateCount=running.Count,
                TerminalBacklogCount=this.terminalResults.Count,
                ActiveRunningCount=this.runningByKey.Count,
                DirtyRunningCount=running.Count,
                TerminalAppliedCount=terminal.Count,
            };
            snapshot=new BenchmarkUiSnapshot(running,terminal,refreshProgress,refreshChart,currentMetrics);
            if(!snapshot.HasUpdates){
                return false;
            }

            Interlocked.Add(ref this.renderedEvents,running.Count+terminal.Count);
            return true;
        }
    }

    public bool HasPending{
        get{
            if(Interlocked.Read(ref this.receivedEvents)!=Interlocked.Read(ref this.consumedEvents)){
                return true;
            }

            lock(this.stateGate){
                return this.dirtyRunningKeys.Count>0||this.terminalResults.Count>0||this.chartDirty;
            }
        }
    }

    public int PendingCount{
        get{
            lock(this.stateGate){
                return this.dirtyRunningKeys.Count+this.terminalResults.Count;
            }
        }
    }

    private async Task ConsumeAsync(){
        Trace.WriteLine("[Pipeline] ConsumeAsync started");
        try{
            await foreach(var item in this.channel.Reader.ReadAllAsync()){
                Interlocked.Increment(ref this.consumedEvents);
                var result = item.Result;
                lock(this.stateGate){
                    if(IsRunning(result)){
                        var key = CreateRunningKey(result);
                        if(this.runningByKey.ContainsKey(key)){
                            Interlocked.Increment(ref this.coalescedRunningEvents);
                        }
                        this.runningByKey[key]=item;
                        this.dirtyRunningKeys.Add(key);
                    }else if(IsTerminal(result)){
                        var key = CreateRunningKey(result);
                        if(this.runningByKey.TryGetValue(key,out var runningItem)){
                            this.terminalPrecedingRunningUpdates.Add(runningItem);
                        }
                        this.runningByKey.Remove(key);
                        this.dirtyRunningKeys.Remove(key);
                        this.terminalResults.Enqueue(item);
                        this.chartDirty=true;
                    }else{
                    }
                }
            }

            Trace.WriteLine("[Pipeline] ConsumeAsync channel completed (reader ended without exception)");
        }catch(Exception ex){
            Trace.WriteLine($"[Pipeline] ConsumeAsync FAULTED: {ex}");
            throw;
        }
    }

    private static bool IsRunning(BenchmarkResultItem result) =>
        string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);

    private static bool IsTerminal(BenchmarkResultItem result) =>
        string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);

    private static string CreateRunningKey(BenchmarkResultItem result) =>
        string.Join("|",
            result.SourceTab,
            result.Project,
            result.TypeFullName,
            result.Method,
            result.ArgumentKey,
            result.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.AssignedLogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-");
}

internal sealed class BenchmarkUiDiagnostics{
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

    public BenchmarkUiDiagnostics(Action<string,Exception?> log){
        this.log=log;
    }

    public void StartHeartbeat(IBenchmarkUiDispatcher dispatcher){
        if(this.heartbeatCancellation is not null){
            return;
        }

        var source = new CancellationTokenSource();
        this.heartbeatCancellation=source;
        _=Task.Run(async () => {
            var expectedAt = DateTimeOffset.UtcNow+TimeSpan.FromMilliseconds(BenchmarkPipelineConstants.UiHeartbeatIntervalMs);
            var lastBudgetReportAt = DateTimeOffset.UtcNow;
            while(!source.IsCancellationRequested){
                try{
                    await Task.Delay(BenchmarkPipelineConstants.UiHeartbeatIntervalMs,source.Token);
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
                        // Trace出力だけではMAUIアプリ実行時にユーザーが確認できないため、startup.logにも残す。
                        this.log("[UI][budget] "+report,null);
                    }
                }

                dispatcher.BeginInvoke(() => {
                    var now = DateTimeOffset.UtcNow;
                    var gap = (now-expectedAt).TotalMilliseconds;
                    expectedAt=now+TimeSpan.FromMilliseconds(BenchmarkPipelineConstants.UiHeartbeatIntervalMs);
                    if(gap>BenchmarkPipelineConstants.UiResponsivenessTargetMs){
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

    public void RecordRender(double elapsedMilliseconds,BenchmarkPipelineMetrics metrics,int renderedCount){
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

    public BenchmarkPipelineMetrics CreateMetrics(int runningUpdateCount,int terminalBacklogCount,int pendingPersistenceEvents) =>
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

    public IReadOnlyList<BenchmarkDiagnosticRow> CreateRows(){
        var metrics = this.CreateMetrics(this.runningUpdateCount,this.terminalBacklogCount,this.pendingPersistenceEvents);
        return [
            new BenchmarkDiagnosticRow("UI","dispatcher gap","target",BenchmarkPipelineConstants.UiResponsivenessTargetMs.ToString(System.Globalization.CultureInfo.InvariantCulture)+"ms",metrics.LastDispatcherGapMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms",metrics.DispatcherStallCount.ToString(System.Globalization.CultureInfo.InvariantCulture),"Last dispatcher delay over heartbeat target."),
            new BenchmarkDiagnosticRow("UI","render batch","running",metrics.RunningUpdateCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.LastRenderMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms",metrics.RenderedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),"Non-blocking 100ms snapshot render."),
            new BenchmarkDiagnosticRow("UI","dirty running","active",metrics.DirtyRunningCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.ActiveRunningCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.TerminalAppliedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),"Dirty running, active running, and terminal items applied in last snapshot."),
            new BenchmarkDiagnosticRow("Event","coalesced running","count",metrics.CoalescedRunningEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.ReceivedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.ConsumedEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),"Old Running updates dropped by key."),
            new BenchmarkDiagnosticRow("Event","terminal backlog","count",metrics.TerminalBacklogCount.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.PendingPersistenceEvents.ToString(System.Globalization.CultureInfo.InvariantCulture),metrics.LastPersistenceMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"ms","Completed/Failed backlog and SQLite batch.")
        ];
    }
}

internal sealed class BenchmarkUiRenderScheduler{
    private readonly IBenchmarkUiDispatcher dispatcher;
    private readonly BenchmarkRunEventPipeline pipeline;
    private readonly BenchmarkUiDiagnostics diagnostics;
    private readonly Action<BenchmarkUiSnapshot> apply;
    private int scheduled;

    public BenchmarkUiRenderScheduler(
        IBenchmarkUiDispatcher dispatcher,
        BenchmarkRunEventPipeline pipeline,
        BenchmarkUiDiagnostics diagnostics,
        Action<BenchmarkUiSnapshot> apply){
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
            await Task.Delay(BenchmarkPipelineConstants.UiRenderIntervalMs);
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

internal sealed class BenchmarkPersistenceBatcher{
    private readonly IBenchmarkUiDispatcher dispatcher;
    private readonly BenchmarkUiDiagnostics diagnostics;
    private readonly Action<IReadOnlyList<BenchmarkPersistenceItem>> persist;
    private readonly ConcurrentQueue<BenchmarkPersistenceItem> queue = new();
    private int scheduled;

    public BenchmarkPersistenceBatcher(
        IBenchmarkUiDispatcher dispatcher,
        BenchmarkUiDiagnostics diagnostics,
        Action<IReadOnlyList<BenchmarkPersistenceItem>> persist){
        this.dispatcher=dispatcher;
        this.diagnostics=diagnostics;
        this.persist=persist;
    }

    public void Enqueue(BenchmarkPersistenceItem item){
        this.queue.Enqueue(item);
        this.RequestFlush(BenchmarkPipelineConstants.PersistenceBatchIntervalMs);
    }

    public void FlushNow() => this.RequestFlush(0);

    private void RequestFlush(int delayMilliseconds){
        if(Interlocked.Exchange(ref this.scheduled,1)==1){
            return;
        }

        _=Task.Run(async () => {
            if(delayMilliseconds>0){
                await Task.Delay(delayMilliseconds);
            }

            this.dispatcher.BeginInvoke(this.FlushOnUiThread);
        });
    }

    private void FlushOnUiThread(){
        Interlocked.Exchange(ref this.scheduled,0);
        if(this.queue.IsEmpty){
            this.diagnostics.RecordPersistence(0,0);
            return;
        }

        var items = new List<BenchmarkPersistenceItem>();
        while(this.queue.TryDequeue(out var item)){
            items.Add(item);
        }

        var stopwatch = Stopwatch.StartNew();
        this.persist(items);
        stopwatch.Stop();
        this.diagnostics.RecordPersistence(stopwatch.Elapsed.TotalMilliseconds,this.queue.Count);
        UiThreadBudget.Record("flush-persist",stopwatch.Elapsed.TotalMilliseconds);
    }
}
