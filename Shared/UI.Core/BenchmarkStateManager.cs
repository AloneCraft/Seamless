using System.Globalization;
using System.Collections.Concurrent;
using System.Text.Json;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkStateManager {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented=true,
    };

    private readonly object fileGate = new();
    private readonly string benchmarkRoot;
    private static readonly ConcurrentDictionary<string,ProgressState> States = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string,string> LatestRuns = new(StringComparer.OrdinalIgnoreCase);

    public BenchmarkStateManager(string benchmarkRoot) {
        this.benchmarkRoot=benchmarkRoot;
    }

    public string BenchmarkRoot => this.benchmarkRoot;

    public string CreateNextRunRoot(){
        Directory.CreateDirectory(this.benchmarkRoot);
        var next = Directory.EnumerateDirectories(this.benchmarkRoot)
            .Select(static path => TryParseRunNumber(path))
            .DefaultIfEmpty(0)
            .Max()+1;
        while(true){
            var runRoot = Path.Combine(this.benchmarkRoot,next.ToString("D6",CultureInfo.InvariantCulture));
            if(!Directory.Exists(runRoot)){
                Directory.CreateDirectory(runRoot);
                return runRoot;
            }

            next++;
        }
    }

    public static int TryParseRunNumber(string runRoot){
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(runRoot));
        return name.Length==6&&int.TryParse(name,NumberStyles.None,CultureInfo.InvariantCulture,out var number)
            ? number
            : 0;
    }

    public ProgressState? LoadLatestUnfinished() {
        if(!LatestRuns.TryGetValue(this.benchmarkRoot,out var runRoot)||string.IsNullOrWhiteSpace(runRoot)) {
            return null;
        }

        var state = this.Load(runRoot);
        return state is not null&&state.HasUnfinishedJobs ? state : null;
    }

    public ProgressState? Load(string runRoot) {
        if(!States.TryGetValue(runRoot,out var state)) {
            return null;
        }

        state=Clone(state);
        var changed = false;
        foreach(var job in state.Jobs.Where(static job => job.Status is JobStatus.Running or JobStatus.Paused)) {
            job.Status=JobStatus.Failed;
            job.FailureReason="Crash/OOM or process termination while job was Running or Paused.";
            job.FinishedAt=DateTimeOffset.UtcNow;
            changed=true;
        }

        if(changed) {
            state.UpdatedAt=DateTimeOffset.UtcNow;
            this.Save(state);
        }

        return state;
    }

    public void Save(ProgressState state) {
        lock(this.fileGate){
            state.UpdatedAt=DateTimeOffset.UtcNow;
            Directory.CreateDirectory(state.RunRoot);
            States[state.RunRoot]=Clone(state);
        }
    }

    public void SaveLatestRun(string runRoot) {
        lock(this.fileGate){
            Directory.CreateDirectory(this.benchmarkRoot);
            LatestRuns[this.benchmarkRoot]=runRoot;
        }
    }

    private static ProgressState Clone(ProgressState source) {
        var json = JsonSerializer.Serialize(source,JsonOptions);
        return JsonSerializer.Deserialize<ProgressState>(json,JsonOptions)
            ??throw new InvalidDataException("Progress state is empty.");
    }
}
