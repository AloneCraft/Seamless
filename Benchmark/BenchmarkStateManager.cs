using System.Text.Json;

namespace Benchmark;

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal sealed class BenchmarkStateManager {
    public const string ProgressFileName = "progress.json";
    public const string LatestRunFileName = "latest-run.json";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented=true,
    };

    private readonly string benchmarkRoot;

    public BenchmarkStateManager(string benchmarkRoot) {
        this.benchmarkRoot=benchmarkRoot;
    }

    public string BenchmarkRoot => this.benchmarkRoot;

    public ProgressState? LoadLatestUnfinished() {
        var marker = this.LoadLatestMarker();
        if(marker is null||string.IsNullOrWhiteSpace(marker.RunRoot)) {
            return null;
        }

        var state = this.Load(marker.RunRoot);
        return state is not null&&state.HasUnfinishedJobs ? state : null;
    }

    public ProgressState? Load(string runRoot) {
        var path = Path.Combine(runRoot,ProgressFileName);
        if(!File.Exists(path)) {
            return null;
        }

        var state = JsonSerializer.Deserialize<ProgressState>(File.ReadAllText(path),JsonOptions)
            ??throw new InvalidDataException("Progress state is empty.");
        var changed = false;
        foreach(var job in state.Jobs.Where(static job => job.Status==JobStatus.Running)) {
            job.Status=JobStatus.Failed;
            job.FailureReason="Crash/OOM or process termination while job was Running.";
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
        state.UpdatedAt=DateTimeOffset.UtcNow;
        Directory.CreateDirectory(state.RunRoot);
        AtomicWrite(Path.Combine(state.RunRoot,ProgressFileName),state);
    }

    public void SaveLatestRun(string runRoot) {
        Directory.CreateDirectory(this.benchmarkRoot);
        AtomicWrite(Path.Combine(this.benchmarkRoot,LatestRunFileName),new LatestRunMarker(runRoot,DateTimeOffset.UtcNow));
    }

    private LatestRunMarker? LoadLatestMarker() {
        var path = Path.Combine(this.benchmarkRoot,LatestRunFileName);
        if(!File.Exists(path)) {
            return null;
        }

        return JsonSerializer.Deserialize<LatestRunMarker>(File.ReadAllText(path),JsonOptions);
    }

    private static void AtomicWrite<T>(string path,T value) {
        var tmpPath = path+".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)??".");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value,JsonOptions);
        using(var stream = new FileStream(tmpPath,FileMode.Create,FileAccess.Write,FileShare.None,bufferSize:8192,FileOptions.WriteThrough)) {
            stream.Write(bytes);
            stream.Flush(flushToDisk:true);
        }

        File.Move(tmpPath,path,overwrite:true);
    }

    private sealed record LatestRunMarker(string RunRoot,DateTimeOffset UpdatedAt);
}
