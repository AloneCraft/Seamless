using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed class RunEventPipeline{
    private readonly Channel<RunEvent> channel = Channel.CreateUnbounded<RunEvent>(new UnboundedChannelOptions{
        SingleReader=true,
        SingleWriter=false,
        AllowSynchronousContinuations=false,
    });
    private readonly object stateGate = new();
    private readonly Dictionary<string,RunEvent> runningByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> dirtyRunningKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RunEvent> terminalPrecedingRunningUpdates = [];
    private readonly Queue<RunEvent> terminalResults = new();
    private long receivedEvents;
    private long consumedEvents;
    private long renderedEvents;
    private long coalescedRunningEvents;
    private bool chartDirty;
    private DateTimeOffset lastProgressSnapshotAt = DateTimeOffset.MinValue;
    private DateTimeOffset lastChartSnapshotAt = DateTimeOffset.MinValue;

    public RunEventPipeline(){
        _=Task.Run(this.ConsumeAsync);
    }

    public void Post(RunEvent item){
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

    public bool TryCreateUiSnapshot(PipelineMetrics metrics,out UiSnapshot snapshot){
        var now = DateTimeOffset.UtcNow;
        lock(this.stateGate){
            var running = new List<RunEvent>(this.dirtyRunningKeys.Count+this.terminalPrecedingRunningUpdates.Count);
            running.AddRange(this.terminalPrecedingRunningUpdates);
            this.terminalPrecedingRunningUpdates.Clear();
            foreach(var key in this.dirtyRunningKeys){
                if(this.runningByKey.TryGetValue(key,out var item)){
                    running.Add(item);
                }
            }
            this.dirtyRunningKeys.Clear();
            var terminal = new List<RunEvent>(PipelineConstants.MaxTerminalResultsPerTick);
            while(terminal.Count<PipelineConstants.MaxTerminalResultsPerTick&&this.terminalResults.Count>0){
                terminal.Add(this.terminalResults.Dequeue());
            }

            var refreshProgress = false;
            if((running.Count>0||terminal.Count>0)&&now-this.lastProgressSnapshotAt>=TimeSpan.FromMilliseconds(PipelineConstants.ProgressRefreshIntervalMs)){
                refreshProgress=true;
                this.lastProgressSnapshotAt=now;
            }

            var refreshChart = false;
            if(this.chartDirty&&now-this.lastChartSnapshotAt>=TimeSpan.FromMilliseconds(PipelineConstants.ChartRefreshIntervalMs)){
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
            snapshot=new UiSnapshot(running,terminal,refreshProgress,refreshChart,currentMetrics);
            if(!snapshot.HasUpdates){
                return false;
            }

            Interlocked.Add(ref this.renderedEvents,running.Count+terminal.Count);
            return true;
        }
    }

    public bool HasPending{
        get{
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
