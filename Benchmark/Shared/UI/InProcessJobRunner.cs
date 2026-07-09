using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class InProcessJobRunner:IBenchmarkJobRunner {
    public Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        if(cancellationToken.IsCancellationRequested) {
            return Task.FromCanceled<BenchmarkJobRunResult>(cancellationToken);
        }

        return Task.Run(() => Run(job,runRoot));
    }

    private static BenchmarkJobRunResult Run(
        BenchmarkJob job,
        string runRoot) {
        var benchmark = job.ToBenchmarkCase();
        var loopCount = Math.Max(1,job.LoopCount);
        var artifactFolder = WorkerClient.CreateArtifactFolder(runRoot,benchmark,job.SourceTab,job.ElementCount,loopCount);
        var logPath = Path.Combine(artifactFolder,"inprocess.log");
        object? instance = null;
        try {
            var assembly = ResolveBenchmarkAssembly(benchmark.AssemblyName);
            var type = assembly.GetType(benchmark.TypeFullName,throwOnError:true,ignoreCase:false)
                ??throw new InvalidOperationException($"Benchmark type was not found: {benchmark.TypeFullName}");
            var method = ResolveBenchmarkMethod(type,benchmark.MethodName);
            var randomSeed = CreateRandomSourceSeed(runRoot,job.SourceTab,benchmark.MethodName,job.ArgumentKey,job.ElementCount,loopCount);
            using var elementScope = BeginElementCountScope(assembly,job.ElementCount,"1");
            using var randomSourceScope = BeginRandomSourceSeedScope(assembly,randomSeed);
            instance=method.IsStatic ? null : Activator.CreateInstance(type);
            var arguments = BenchmarkArgumentFormatting.ToObjects(benchmark.Arguments);
            var resultDigest = CaptureBenchmarkResultDigest(type,benchmark.MethodName,arguments);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for(var index = 0;index<loopCount;index++) {
                method.Invoke(instance,arguments);
            }
            stopwatch.Stop();
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            var meanNanoseconds = stopwatch.Elapsed.TotalMilliseconds*1_000_000;
            var allocatedBytes = Math.Max(0,allocatedAfter-allocatedBefore);
            WriteInProcessLog(
                logPath,
                $"In-process direct benchmark completed in {BenchmarkFormatting.FormatNanoseconds(meanNanoseconds)}.",
                Encoding.UTF8);
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
                allocatedBytes,
                0,
                artifactFolder,
                string.Empty,
                randomSeed,
                resultDigest,
                benchmark.ArgumentKey,
                benchmark.Arguments,
                loopCount,
                job.RunItemIndex);
        } catch(Exception ex) when(ex is not OperationCanceledException) {
            var actual = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            WriteInProcessLog(logPath,actual.ToString(),Encoding.UTF8);
            return new BenchmarkJobRunResult(
                benchmark.Project,
                benchmark.MethodName,
                benchmark.TypeFullName,
                job.ElementCount,
                "Failed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                artifactFolder,
                actual.ToString(),
                null,
                string.Empty,
                benchmark.ArgumentKey,
                benchmark.Arguments,
                loopCount,
                job.RunItemIndex);
        } finally {
            if(instance is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    private static void WriteInProcessLog(string logPath,string text,Encoding encoding) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)??".");
            File.WriteAllText(logPath,text,encoding);
        } catch(IOException ex) {
            System.Diagnostics.Debug.WriteLine("In-process benchmark log write failed. "+ex);
        } catch(UnauthorizedAccessException ex) {
            System.Diagnostics.Debug.WriteLine("In-process benchmark log write failed. "+ex);
        }
    }

    private static Assembly ResolveBenchmarkAssembly(string assemblyName) =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(item => string.Equals(item.GetName().Name,assemblyName,StringComparison.OrdinalIgnoreCase))
        ??Assembly.Load(new AssemblyName(assemblyName));

    private static MethodInfo ResolveBenchmarkMethod(Type type,string methodName) {
        var method = type.GetMethod(methodName,BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)
            ??throw new MissingMethodException(type.FullName,methodName);
        if(!HasBenchmarkAttribute(method)) {
            throw new MissingMethodException(type.FullName,methodName+" [Benchmark]");
        }

        return method;
    }

    private static bool HasBenchmarkAttribute(MethodInfo method) =>
        method.CustomAttributes.Any(static attribute => attribute.AttributeType.Name=="BenchmarkAttribute");

    private static IDisposable BeginElementCountScope(Assembly assembly,int elementCount,string elementScale) {
        var modeType = assembly.GetType("Common.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false);
        var method = modeType?.GetMethod(
            "UseElementCount",
            BindingFlags.Public|BindingFlags.Static,
            binder:null,
            types:[typeof(int),typeof(string)],
            modifiers:null);
        return method?.Invoke(null,[elementCount,elementScale]) as IDisposable
            ?? EmptyDisposable.Instance;
    }

    private static IDisposable BeginRandomSourceSeedScope(Assembly assembly,int seed) {
        var modeType = assembly.GetType("Common.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false);
        var method = modeType?.GetMethod(
            "UseRandomSourceSeed",
            BindingFlags.Public|BindingFlags.Static,
            binder:null,
            types:[typeof(int)],
            modifiers:null);
        return method?.Invoke(null,[seed]) as IDisposable
            ?? EmptyDisposable.Instance;
    }

    private static string CaptureBenchmarkResultDigest(Type benchmarkType,string methodName,object?[] arguments) {
        var instance = Activator.CreateInstance(benchmarkType);
        var method = benchmarkType.GetMethod(
            "CaptureBenchmarkMethodResult",
            BindingFlags.Public|BindingFlags.Instance,
            binder:null,
            types:[typeof(string),typeof(bool),typeof(object[])],
            modifiers:null)
            ??throw new MissingMethodException(benchmarkType.FullName,"CaptureBenchmarkMethodResult");
        return method.Invoke(instance,[methodName,false,arguments]) as string
            ??throw new InvalidOperationException("Benchmark result digest was empty.");
    }

    private static int CreateRandomSourceSeed(string runRoot,BenchmarkSourceTab sourceTab,string methodName,string argumentKey,int elementCount,int loopCount) =>
        StableHash(runRoot+"|"+sourceTab+"|"+methodName+"|"+argumentKey+"|"+elementCount.ToString(CultureInfo.InvariantCulture)+"|"+loopCount.ToString(CultureInfo.InvariantCulture));

    private static int StableHash(string text) {
        unchecked {
            var hash = (int)2166136261;
            foreach(var ch in text) {
                hash^=ch;
                hash*=16777619;
            }
            return hash==0 ? 1 : hash;
        }
    }

    private sealed class EmptyDisposable:IDisposable {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose() {
        }
    }
}
