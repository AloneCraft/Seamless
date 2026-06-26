using System.Text;

namespace Benchmark;

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal sealed class BenchmarkResultWriter {
    private readonly object writeGate = new();

    public IReadOnlyList<string> WriteOutputs(ProgressState state) {
        lock(this.writeGate){
            Directory.CreateDirectory(state.RunRoot);
            var paths = new List<string>();
            foreach(var group in state.Jobs
                .Where(static job => job.Status==JobStatus.Completed)
                .GroupBy(static job => job.ElementCount)
                .OrderBy(static group => group.Key)) {
                var rows = BenchmarkComparisonFormatter.BuildComparisonRows(group
                    .Select(static job => job.ToReportRow())
                    .ToArray());
                var path = Path.Combine(state.RunRoot,$"BenchmarkComparison-N{group.Key}.csv");
                AtomicWriteText(path,BenchmarkComparisonFormatter.WriteCsv(rows),new UTF8Encoding(true));
                paths.Add(path);
            }

            this.WriteSummary(state,paths);
            return paths;
        }
    }

    private void WriteSummary(ProgressState state,IReadOnlyList<string> csvPaths) {
        var builder = new StringBuilder();
        builder.AppendLine($"RunId: {state.RunId}");
        builder.AppendLine($"Mode: {state.Mode}");
        builder.AppendLine($"ExecutionProfile: {state.ExecutionProfile}");
        builder.AppendLine($"AllocatedBytesSource: {BenchmarkExecutionProfileNames.GetAllocatedBytesSource(state.ExecutionProfile)}");
        builder.AppendLine($"Jobs: {state.Jobs.Count}");
        builder.AppendLine($"Completed: {state.Jobs.Count(static job => job.Status==JobStatus.Completed)}");
        builder.AppendLine($"Failed: {state.Jobs.Count(static job => job.Status==JobStatus.Failed)}");
        builder.AppendLine($"Pending: {state.Jobs.Count(static job => job.Status==JobStatus.Pending)}");
        builder.AppendLine($"Running: {state.Jobs.Count(static job => job.Status==JobStatus.Running)}");
        foreach(var path in csvPaths) {
            builder.AppendLine($"CsvReport: {path}");
        }

        AtomicWriteText(Path.Combine(state.RunRoot,"BenchmarkRunSummary.txt"),builder.ToString(),Encoding.UTF8);
    }

    private static void AtomicWriteText(string path,string text,Encoding encoding) {
        var tmpPath = path+"."+Guid.NewGuid().ToString("N")+".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)??".");
        try{
            using(var stream = new FileStream(tmpPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,bufferSize:8192,FileOptions.WriteThrough))
            using(var writer = new StreamWriter(stream,encoding)) {
                writer.Write(text);
                writer.Flush();
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
}
