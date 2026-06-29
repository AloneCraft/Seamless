using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

Console.OutputEncoding=Encoding.UTF8;
Console.InputEncoding=Encoding.UTF8;
const string ElementScaleEnvironmentVariable = "SEAMLESS_BENCHMARK_ELEMENT_SCALE";
const string ElementCountEnvironmentVariable = "SEAMLESS_BENCHMARK_ELEMENT_COUNT";
const string PreparedElementCountsEnvironmentVariable = "SEAMLESS_BENCHMARK_PREPARED_ELEMENT_COUNTS";
const string RandomSourceSeedEnvironmentVariable = "SEAMLESS_BENCHMARK_RANDOM_SOURCE_SEED";

var logicalCpu = ReadRequiredIntArg(args,"--logical-cpu");
var runRoot = ReadRequiredStringArg(args,"--run-root");
Directory.CreateDirectory(runRoot);
ApplyAffinity(logicalCpu);

var protocolOutput = Console.Out;
var protocolError = Console.Error;
await protocolOutput.WriteLineAsync("ready");
await protocolOutput.FlushAsync();
while(await Console.In.ReadLineAsync() is { } line){
    line=TrimUtf8Bom(line);
    if(string.IsNullOrWhiteSpace(line)){
        continue;
    }

    WorkerTaskMessage? task = null;
    try{
        task=JsonSerializer.Deserialize<WorkerTaskMessage>(line);
        if(task is null){
            continue;
        }

        if(string.Equals(task.Command,"shutdown",StringComparison.OrdinalIgnoreCase)){
            return 0;
        }

        var result = RunBenchmark(task,logicalCpu,protocolOutput,protocolError);
        await protocolOutput.WriteLineAsync(JsonSerializer.Serialize(result));
        await protocolOutput.FlushAsync();
    }catch(Exception ex){
        var result = new WorkerResultMessage(
            task?.TaskId??string.Empty,
            "Failed",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            1,
            task?.ArtifactFolder??string.Empty,
            ex.ToString(),
            null,
            string.Empty);
        await protocolOutput.WriteLineAsync(JsonSerializer.Serialize(result));
        await protocolOutput.FlushAsync();
    }
}

return 0;

static WorkerResultMessage RunBenchmark(
    WorkerTaskMessage task,
    int logicalCpu,
    TextWriter protocolOutput,
    TextWriter protocolError){
    Directory.CreateDirectory(task.ArtifactFolder);
    var logPath = Path.Combine(task.ArtifactFolder,"worker.log");

    using var logWriter = new StringWriter(CultureInfo.InvariantCulture);
    var originalOut = Console.Out;
    var originalError = Console.Error;
    var previousScale = Environment.GetEnvironmentVariable(ElementScaleEnvironmentVariable);
    var previousElementCount = Environment.GetEnvironmentVariable(ElementCountEnvironmentVariable);
    var previousPreparedElementCounts = Environment.GetEnvironmentVariable(PreparedElementCountsEnvironmentVariable);
    var previousRandomSourceSeed = Environment.GetEnvironmentVariable(RandomSourceSeedEnvironmentVariable);
    Console.SetOut(logWriter);
    Console.SetError(logWriter);
    try{
        ApplyAffinity(logicalCpu);
        var randomSeed = CreateRandomSourceSeed(task);
        Environment.SetEnvironmentVariable(ElementScaleEnvironmentVariable,string.IsNullOrWhiteSpace(task.ElementScale)?"1":task.ElementScale);
        Environment.SetEnvironmentVariable(
            ElementCountEnvironmentVariable,
            task.ElementCount>0?task.ElementCount.ToString(CultureInfo.InvariantCulture):null);
        Environment.SetEnvironmentVariable(
            PreparedElementCountsEnvironmentVariable,
            task.PreparedElementCounts.Length>0
                ? string.Join(",",task.PreparedElementCounts.Distinct().Order().Select(static count => count.ToString(CultureInfo.InvariantCulture)))
                : null);
        Environment.SetEnvironmentVariable(RandomSourceSeedEnvironmentVariable,randomSeed.ToString(CultureInfo.InvariantCulture));
        var benchmarkType = ResolveBenchmarkType(task.AssemblyName,task.TypeFullName);
        using var elementScope = BeginElementCountScope(benchmarkType.Assembly,task.ElementCount,task.ElementScale);
        using var randomSourceScope = BeginRandomSourceSeedScope(benchmarkType.Assembly,randomSeed);
        var resultDigest = CaptureBenchmarkResultDigest(benchmarkType,task.MethodName);
        var config = BenchmarkWorkerRuntime.CreateConfig(task.ArtifactFolder);
        var summaries = BenchmarkSwitcher
            .FromTypes([benchmarkType])
            .Run(["--filter","*."+task.MethodName],config);
        var summary = summaries.FirstOrDefault();
        var report = summary?.Reports.FirstOrDefault();
        var stats = report?.ResultStatistics;
        var allocatedBytes = GetAllocatedBytes(report);
        var log = logWriter.ToString();
        File.WriteAllText(logPath,log,Encoding.UTF8);
        if(!BenchmarkWorkerRuntime.HasUsableStatistics(stats?.Mean)){
            return new WorkerResultMessage(
                task.TaskId,
                "Failed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                task.ArtifactFolder,
                string.IsNullOrWhiteSpace(log)
                    ? "BenchmarkDotNet did not produce result statistics."
                    : log,
                randomSeed,
                resultDigest);
        }

        return new WorkerResultMessage(
            task.TaskId,
            "Completed",
            FormatNanoseconds(stats?.Mean),
            FormatNanoseconds(stats?.StandardError),
            FormatNanoseconds(stats?.StandardDeviation),
            stats?.Mean,
            stats?.StandardError,
            stats?.StandardDeviation,
            allocatedBytes,
            0,
            task.ArtifactFolder,
            log,
            randomSeed,
            resultDigest);
    }finally{
        Environment.SetEnvironmentVariable(ElementScaleEnvironmentVariable,previousScale);
        Environment.SetEnvironmentVariable(ElementCountEnvironmentVariable,previousElementCount);
        Environment.SetEnvironmentVariable(PreparedElementCountsEnvironmentVariable,previousPreparedElementCounts);
        Environment.SetEnvironmentVariable(RandomSourceSeedEnvironmentVariable,previousRandomSourceSeed);
        Console.SetOut(protocolOutput);
        Console.SetError(protocolError);
    }
}

static Type ResolveBenchmarkType(string assemblyName,string typeFullName){
    var assembly = AppDomain.CurrentDomain
        .GetAssemblies()
        .FirstOrDefault(item => string.Equals(item.GetName().Name,assemblyName,StringComparison.OrdinalIgnoreCase))
        ?? Assembly.Load(new AssemblyName(assemblyName));
    return assembly.GetType(typeFullName,throwOnError:true,ignoreCase:false)
        ??throw new InvalidOperationException($"Benchmark type was not found: {typeFullName}");
}

static IDisposable BeginElementCountScope(Assembly assembly,int elementCount,string elementScale){
    var modeType = assembly.GetType("Benchmark.Shared.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false);
    var method = modeType?.GetMethod(
        "UseElementCount",
        BindingFlags.Public|BindingFlags.Static,
        binder:null,
        types:[typeof(int),typeof(string)],
        modifiers:null);
    return method?.Invoke(null,[elementCount,string.IsNullOrWhiteSpace(elementScale)?"1":elementScale]) as IDisposable
        ?? EmptyDisposable.Instance;
}

static IDisposable BeginRandomSourceSeedScope(Assembly assembly,int seed){
    var modeType = assembly.GetType("Benchmark.Shared.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false);
    var method = modeType?.GetMethod(
        "UseRandomSourceSeed",
        BindingFlags.Public|BindingFlags.Static,
        binder:null,
        types:[typeof(int)],
        modifiers:null);
    return method?.Invoke(null,[seed]) as IDisposable
        ?? EmptyDisposable.Instance;
}

static string CaptureBenchmarkResultDigest(Type benchmarkType,string methodName){
    var instance = Activator.CreateInstance(benchmarkType);
    var method = benchmarkType.GetMethod(
        "CaptureBenchmarkMethodResult",
        BindingFlags.Public|BindingFlags.Instance,
        binder:null,
        types:[typeof(string),typeof(bool)],
        modifiers:null)
        ??throw new MissingMethodException(benchmarkType.FullName,"CaptureBenchmarkMethodResult");
    return method.Invoke(instance,[methodName,false]) as string
        ??throw new InvalidOperationException("Benchmark result digest was empty.");
}

static int CreateRandomSourceSeed(WorkerTaskMessage task){
    var runRoot = TryGetRunRoot(task.ArtifactFolder);
    return StableHash(runRoot+"|"+task.SourceTab+"|"+task.MethodName+"|"+task.ElementCount.ToString(CultureInfo.InvariantCulture));
}

static string TryGetRunRoot(string artifactFolder){
    var directory = new DirectoryInfo(artifactFolder);
    for(var i=0;i<4&&directory is not null;i++){
        directory=directory.Parent;
    }
    return directory?.FullName??artifactFolder;
}

static int StableHash(string text){
    unchecked{
        var hash = (int)2166136261;
        foreach(var ch in text){
            hash^=ch;
            hash*=16777619;
        }
        return hash==0 ? 1 : hash;
    }
}

static string FormatNanoseconds(double? value){
    return value is { } number&&double.IsFinite(number)
        ? number.ToString("N3",CultureInfo.InvariantCulture)+" ns"
        : string.Empty;
}

static long? GetAllocatedBytes(object? report){
    var gcStats = report?.GetType().GetProperty("GcStats")?.GetValue(report);
    if(gcStats is null){
        return null;
    }

    var totalMethod = gcStats.GetType().GetMethod("GetTotalAllocatedBytes",new[] { typeof(bool) });
    var totalIncludingQuantum = ToNonNegativeLong(totalMethod?.Invoke(gcStats,new object[] { false }));
    var totalExcludingQuantum = ToNonNegativeLong(totalMethod?.Invoke(gcStats,new object[] { true }));
    var allocatedBytes = ToNonNegativeLong(gcStats.GetType().GetProperty("AllocatedBytes")?.GetValue(gcStats));
    foreach(var value in new[] { totalIncludingQuantum,allocatedBytes,totalExcludingQuantum }){
        if(value is >0){
            return value;
        }
    }

    return totalIncludingQuantum>=0
        ? totalIncludingQuantum
        : allocatedBytes>=0
            ? allocatedBytes
            : totalExcludingQuantum>=0
                ? totalExcludingQuantum
                : null;
}

static long? ToNonNegativeLong(object? value) =>
    value is long number&&number>=0
        ? number
        : null;

static string TrimUtf8Bom(string text){
    return text.Length>0&&text[0]=='\uFEFF'
        ? text[1..]
        : text;
}

static int ReadRequiredIntArg(string[] args,string name){
    var value = ReadRequiredStringArg(args,name);
    return int.Parse(value,CultureInfo.InvariantCulture);
}

static string ReadRequiredStringArg(string[] args,string name){
    for(var i=0;i<args.Length-1;i++){
        if(string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase)){
            return args[i+1];
        }
    }

    throw new ArgumentException($"Missing required argument: {name}");
}

static void ApplyAffinity(int logicalCpu){
    if(logicalCpu<0||logicalCpu>=Environment.ProcessorCount||logicalCpu>=63){
        return;
    }

    var mask = 1L<<logicalCpu;
    if(OperatingSystem.IsWindows()){
        try{
            Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(mask);
        }catch(PlatformNotSupportedException){
        }catch(InvalidOperationException){
        }catch(System.ComponentModel.Win32Exception){
        }

        SetThreadAffinityMask(GetCurrentThread(),new UIntPtr((ulong)mask));
    }else if(OperatingSystem.IsLinux()){
        try{
            Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(mask);
        }catch(PlatformNotSupportedException){
        }catch(InvalidOperationException){
        }catch(System.ComponentModel.Win32Exception){
        }
    }
}

[DllImport("kernel32.dll")]
static extern IntPtr GetCurrentThread();

[DllImport("kernel32.dll",SetLastError=true)]
static extern UIntPtr SetThreadAffinityMask(IntPtr threadHandle,UIntPtr threadAffinityMask);

internal sealed record WorkerTaskMessage(
    string Command,
    string TaskId,
    string AssemblyName,
    string TypeFullName,
    string MethodName,
    BenchmarkSourceTab SourceTab,
    int[] PreparedElementCounts,
    int ElementCount,
    string ElementScale,
    string ArtifactFolder);

internal enum BenchmarkSourceTab{
    SystemArray,
    SystemRange,
    SystemList,
    SeamlessPersisted,
    SeamlessTsavorite,
}

internal sealed record WorkerResultMessage(
    string TaskId,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    double? MeanNanoseconds,
    double? ErrorNanoseconds,
    double? StdDevNanoseconds,
    long? AllocatedBytes,
    int ExitCode,
    string ArtifactFolder,
    string Log,
    int? RandomSeed,
    string ResultDigest);
internal sealed class EmptyDisposable:IDisposable{
    public static EmptyDisposable Instance { get; } = new();

    public void Dispose(){
    }
}
