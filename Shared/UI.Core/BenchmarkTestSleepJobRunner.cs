using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace UI;

public sealed class BenchmarkTestSleepJobRunner(TimeSpan sleepDuration,bool runUntilCanceled = false):IBenchmarkJobRunner{
    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken){
        var benchmark = job.ToBenchmarkCase();
        var loopCount = Math.Max(1,job.LoopCount);
        var artifactFolder = CreateArtifactFolder(runRoot,benchmark,job.SourceTab,job.ElementCount,loopCount);
        var effectiveSleepDuration = sleepDuration<=TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : sleepDuration;
        var stopwatch = Stopwatch.StartNew();
        if(runUntilCanceled){
            await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);
        }else{
            await Task.Delay(effectiveSleepDuration,cancellationToken);
        }
        stopwatch.Stop();
        var meanNanoseconds = stopwatch.Elapsed.TotalMilliseconds*1_000_000;
        var log = "Test benchmark job slept for "+BenchmarkFormatting.FormatNanoseconds(meanNanoseconds)+".";
        WriteTestLog(artifactFolder,log);
        return new BenchmarkJobRunResult(
            benchmark.Project,
            benchmark.MethodName,
            benchmark.TypeFullName,
            job.ElementCount,
            "Completed",
            BenchmarkFormatting.FormatNanoseconds(meanNanoseconds),
            string.Empty,
            string.Empty,
            meanNanoseconds,
            null,
            null,
            0,
            0,
            artifactFolder,
            log,
            null,
            "test",
            benchmark.ArgumentKey,
            benchmark.Arguments,
            loopCount,
            job.RunItemIndex);
    }

    private static string CreateArtifactFolder(string runRoot,BenchmarkCase benchmark,BenchmarkSourceTab sourceTab,int elementCount,int loopCount){
        var artifactFolder = Path.Combine(
            runRoot,
            BenchmarkSourceTabs.ToPathPart(sourceTab),
            "N"+elementCount.ToString(CultureInfo.InvariantCulture),
            "Loop"+Math.Max(1,loopCount).ToString(CultureInfo.InvariantCulture),
            SanitizePathPart(benchmark.Project),
            SanitizePathPart(benchmark.TypeFullName),
            SanitizePathPart(benchmark.MethodName),
            string.IsNullOrWhiteSpace(benchmark.ArgumentKey) ? "default" : SanitizePathPart(benchmark.ArgumentKey));
        Directory.CreateDirectory(artifactFolder);
        return artifactFolder;
    }

    private static string SanitizePathPart(string value){
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach(var ch in value){
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.Length==0 ? "_" : builder.ToString();
    }

    private static void WriteTestLog(string artifactFolder,string text){
        try{
            Directory.CreateDirectory(artifactFolder);
            File.WriteAllText(Path.Combine(artifactFolder,"test.log"),text,Encoding.UTF8);
        }catch(IOException ex){
            Trace.WriteLine("Test benchmark log write failed. "+ex);
        }catch(UnauthorizedAccessException ex){
            Trace.WriteLine("Test benchmark log write failed. "+ex);
        }
    }
}
