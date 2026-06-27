using System.Globalization;
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

    private readonly object fileGate = new();
    private readonly string benchmarkRoot;

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
            AtomicWrite(Path.Combine(state.RunRoot,ProgressFileName),state);
        }
    }

    public void SaveLatestRun(string runRoot) {
        lock(this.fileGate){
            Directory.CreateDirectory(this.benchmarkRoot);
            AtomicWrite(Path.Combine(this.benchmarkRoot,LatestRunFileName),new LatestRunMarker(runRoot,DateTimeOffset.UtcNow));
        }
    }

    private LatestRunMarker? LoadLatestMarker() {
        var path = Path.Combine(this.benchmarkRoot,LatestRunFileName);
        if(!File.Exists(path)) {
            return null;
        }

        return JsonSerializer.Deserialize<LatestRunMarker>(File.ReadAllText(path),JsonOptions);
    }

    private static void AtomicWrite<T>(string path,T value) {
        var tmpPath = path+"."+Guid.NewGuid().ToString("N")+".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)??".");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value,JsonOptions);
        try{
            using(var stream = new FileStream(tmpPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,bufferSize:8192,FileOptions.WriteThrough)) {
                stream.Write(bytes);
                stream.Flush(flushToDisk:true);
            }

            File.Move(tmpPath,path,overwrite:true);
        }finally{
            TryDelete(tmpPath);
        }
    }

    private static void TryDelete(string path){
        try{
            if(File.Exists(path)){
                File.Delete(path);
            }
        }catch(IOException){
        }catch(UnauthorizedAccessException){
        }
    }

    private sealed record LatestRunMarker(string RunRoot,DateTimeOffset UpdatedAt);
}
