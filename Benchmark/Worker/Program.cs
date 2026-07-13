using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

Console.OutputEncoding=Encoding.UTF8;
Console.InputEncoding=Encoding.UTF8;
const string ElementScaleEnvironmentVariable = "SEAMLESS_BENCHMARK_ELEMENT_SCALE";
const string ElementCountEnvironmentVariable = "SEAMLESS_BENCHMARK_ELEMENT_COUNT";
const string LoopCountEnvironmentVariable = "SEAMLESS_BENCHMARK_LOOP_COUNT";
const string PreparedElementCountsEnvironmentVariable = "SEAMLESS_BENCHMARK_PREPARED_ELEMENT_COUNTS";
const string RandomSourceSeedEnvironmentVariable = "SEAMLESS_BENCHMARK_RANDOM_SOURCE_SEED";

var logicalCpu = ReadRequiredIntArg(args,"--logical-cpu");
var runRoot = ReadRequiredStringArg(args,"--run-root");
Directory.CreateDirectory(runRoot);
ApplyAffinity(logicalCpu);

var protocolOutput = Console.Out;
var protocolError = Console.Error;
var protocolWriteGate = new SemaphoreSlim(1,1);
var fastTasks = new ConcurrentBag<Task>();
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
            await Task.WhenAll(fastTasks.ToArray());
            return 0;
        }

        if(string.Equals(task.Command,"fast-run",StringComparison.OrdinalIgnoreCase)){
            var fastTask = task;
            fastTasks.Add(Task.Run(async () => {
                WorkerResultMessage result;
                try{
                    result=RunFastBenchmark(fastTask,fastTask.AssignedLogicalCpu??logicalCpu,protocolOutput,protocolError);
                }catch(Exception ex){
                    result=new WorkerResultMessage(
                        fastTask.TaskId,
                        "Failed",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        null,
                        null,
                        null,
                        null,
                        1,
                        fastTask.ArtifactFolder,
                        ex.ToString(),
                        null,
                        string.Empty);
                }
                await protocolWriteGate.WaitAsync();
                try{
                    await protocolOutput.WriteLineAsync(JsonSerializer.Serialize(result));
                    await protocolOutput.FlushAsync();
                }finally{
                    protocolWriteGate.Release();
                }
            }));
            continue;
        }

        var result = RunBenchmark(task,logicalCpu,protocolOutput,protocolError);
        await protocolWriteGate.WaitAsync();
        try{
            await protocolOutput.WriteLineAsync(JsonSerializer.Serialize(result));
            await protocolOutput.FlushAsync();
        }finally{
            protocolWriteGate.Release();
        }
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
        await protocolWriteGate.WaitAsync();
        try{
            await protocolOutput.WriteLineAsync(JsonSerializer.Serialize(result));
            await protocolOutput.FlushAsync();
        }finally{
            protocolWriteGate.Release();
        }
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
    var previousLoopCount = Environment.GetEnvironmentVariable(LoopCountEnvironmentVariable);
    var previousPreparedElementCounts = Environment.GetEnvironmentVariable(PreparedElementCountsEnvironmentVariable);
    var previousRandomSourceSeed = Environment.GetEnvironmentVariable(RandomSourceSeedEnvironmentVariable);
    Console.SetOut(logWriter);
    Console.SetError(logWriter);
    try{
        ApplyAffinity(logicalCpu);
        var randomSeed = CreateRandomSourceSeed(task);
        var loopCount = Math.Max(1,task.LoopCount);
        Environment.SetEnvironmentVariable(ElementScaleEnvironmentVariable,string.IsNullOrWhiteSpace(task.ElementScale)?"1":task.ElementScale);
        Environment.SetEnvironmentVariable(
            ElementCountEnvironmentVariable,
            task.ElementCount>0?task.ElementCount.ToString(CultureInfo.InvariantCulture):null);
        Environment.SetEnvironmentVariable(LoopCountEnvironmentVariable,loopCount.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable(
            PreparedElementCountsEnvironmentVariable,
            task.PreparedElementCounts.Length>0
                ? string.Join(",",task.PreparedElementCounts.Distinct().Order().Select(static count => count.ToString(CultureInfo.InvariantCulture)))
                : null);
        Environment.SetEnvironmentVariable(RandomSourceSeedEnvironmentVariable,randomSeed.ToString(CultureInfo.InvariantCulture));
        var benchmarkType = ResolveBenchmarkType(task.AssemblyName,task.TypeFullName);
        using var elementScope = BeginElementCountScope(benchmarkType.Assembly,task.ElementCount,task.ElementScale);
        using var randomSourceScope = BeginRandomSourceSeedScope(benchmarkType.Assembly,randomSeed);
        var argumentValues = BenchmarkArgumentFormatting.ToObjects(task.Arguments);
        var resultDigest = CaptureBenchmarkResultDigest(benchmarkType,task.MethodName,argumentValues);
        var config = ManualConfig
            .Create(BenchmarkWorkerRuntime.CreateConfig(task.ArtifactFolder,loopCount))
            .AddFilter(new WorkerTaskFilter(task));
        var summaries = BenchmarkSwitcher
            .FromTypes([benchmarkType])
            .Run(["--filter","*."+task.MethodName+"*"],config);
        var summary = summaries.SingleOrDefault()
            ??throw new InvalidOperationException($"BenchmarkDotNet did not return a summary for {task.TypeFullName}.{task.MethodName}.");
        var reports = summary.Reports
            .Where(report => IsReportForArguments(report.BenchmarkCase,task.Arguments))
            .ToArray();
        if(reports.Length!=1){
            throw new InvalidOperationException($"BenchmarkDotNet returned {reports.Length} reports for {task.TypeFullName}.{task.MethodName} with arguments '{task.ArgumentKey}'.");
        }
        var report = reports[0];
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

        var totalMean = stats?.Mean*loopCount;
        var totalError = stats?.StandardError*loopCount;
        var totalStdDev = stats?.StandardDeviation*loopCount;
        long? totalAllocatedBytes = allocatedBytes is { } bytes ? bytes*loopCount : null;
        return new WorkerResultMessage(
            task.TaskId,
            "Completed",
            FormatNanoseconds(totalMean),
            FormatNanoseconds(totalError),
            FormatNanoseconds(totalStdDev),
            totalMean,
            totalError,
            totalStdDev,
            totalAllocatedBytes,
            0,
            task.ArtifactFolder,
            log,
            randomSeed,
            resultDigest);
    }finally{
        Environment.SetEnvironmentVariable(ElementScaleEnvironmentVariable,previousScale);
        Environment.SetEnvironmentVariable(ElementCountEnvironmentVariable,previousElementCount);
        Environment.SetEnvironmentVariable(LoopCountEnvironmentVariable,previousLoopCount);
        Environment.SetEnvironmentVariable(PreparedElementCountsEnvironmentVariable,previousPreparedElementCounts);
        Environment.SetEnvironmentVariable(RandomSourceSeedEnvironmentVariable,previousRandomSourceSeed);
        Console.SetOut(protocolOutput);
        Console.SetError(protocolError);
    }
}

static WorkerResultMessage RunFastBenchmark(
    WorkerTaskMessage task,
    int logicalCpu,
    TextWriter protocolOutput,
    TextWriter protocolError){
    Directory.CreateDirectory(task.ArtifactFolder);
    var logPath = Path.Combine(task.ArtifactFolder,"fast-worker.log");
    object? instance = null;
    try{
        ApplyThreadAffinity(logicalCpu);
        var benchmarkType = ResolveBenchmarkType(task.AssemblyName,task.TypeFullName);
        var method = ResolveBenchmarkMethod(benchmarkType,task.MethodName);
        var randomSeed = CreateRandomSourceSeed(task);
        var loopCount = Math.Max(1,task.LoopCount);
        using var elementScope = BeginElementCountScope(benchmarkType.Assembly,task.ElementCount,task.ElementScale);
        using var randomSourceScope = BeginRandomSourceSeedScope(benchmarkType.Assembly,randomSeed);
        instance=method.IsStatic ? null : Activator.CreateInstance(benchmarkType);
        var argumentValues = BenchmarkArgumentFormatting.ToObjects(task.Arguments);
        var resultDigest = CaptureBenchmarkResultDigest(benchmarkType,task.MethodName,argumentValues);
        var invokeBenchmark = argumentValues.Length==0
            ? method.IsStatic
                ? (Action)method.CreateDelegate(typeof(Action))
                : (Action)method.CreateDelegate(typeof(Action),instance)
            : () => method.Invoke(instance,argumentValues);
        invokeBenchmark();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for(var index=0;index<loopCount;index++){
            invokeBenchmark();
        }
        stopwatch.Stop();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        var meanNanoseconds = stopwatch.Elapsed.TotalMilliseconds*1_000_000d;
        var allocatedBytes = Math.Max(0,allocatedAfter-allocatedBefore);
        var counter = new global::Seamless.Linq.LinqCounter();
        using(var counterScope = global::Seamless.Linq.LinqCounterScope.Start(counter)) {
            invokeBenchmark();
        }
        var diagnosticCounters = CreateDiagnosticCounters(counter);
        File.WriteAllText(
            logPath,
            "Fast worker direct benchmark completed. CPU="
            +(logicalCpu>=0 ? logicalCpu.ToString(CultureInfo.InvariantCulture) : "-")
            +", Elapsed="+FormatNanoseconds(meanNanoseconds)
            +", AllocatedBytes="+allocatedBytes.ToString(CultureInfo.InvariantCulture),
            Encoding.UTF8);
        return new WorkerResultMessage(
            task.TaskId,
            "Completed",
            FormatNanoseconds(meanNanoseconds),
            string.Empty,
            string.Empty,
            meanNanoseconds,
            null,
            null,
            allocatedBytes,
            0,
            task.ArtifactFolder,
            string.Empty,
            randomSeed,
            resultDigest,
            diagnosticCounters);
    }catch(Exception ex) when(ex is not OperationCanceledException){
        var actual = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
        Directory.CreateDirectory(task.ArtifactFolder);
        File.WriteAllText(logPath,actual.ToString(),Encoding.UTF8);
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
            actual.ToString(),
            null,
            string.Empty);
    }finally{
        if(instance is IDisposable disposable){
            disposable.Dispose();
        }
        Console.SetOut(protocolOutput);
        Console.SetError(protocolError);
    }
}

static Type ResolveBenchmarkType(string assemblyName,string typeFullName){
    var assembly = AppDomain.CurrentDomain
        .GetAssemblies()
        .FirstOrDefault(item => string.Equals(item.GetName().Name,assemblyName,StringComparison.OrdinalIgnoreCase))
        ?? Assembly.Load(new AssemblyName(assemblyName));
    return assembly.GetType(typeFullName,throwOnError:false,ignoreCase:false)
        ??throw new TypeLoadException($"Benchmark type was not found: {assemblyName}:{typeFullName}");
}

static MethodInfo ResolveBenchmarkMethod(Type benchmarkType,string methodName){
    var methods = benchmarkType.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static);
    var method = methods.SingleOrDefault(method =>
        string.Equals(method.Name,methodName,StringComparison.Ordinal)
        &&HasBenchmarkAttribute(method));
    return method??throw new MissingMethodException(benchmarkType.FullName,methodName);
}

static bool HasBenchmarkAttribute(MethodInfo method) =>
    method.GetCustomAttributes(inherit:true).Any(static attribute =>
        string.Equals(attribute.GetType().Name,"BenchmarkAttribute",StringComparison.Ordinal));

static IDisposable BeginElementCountScope(Assembly assembly,int elementCount,string elementScale){
    var modeType = assembly.GetType("Common.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false)
        ??AppDomain.CurrentDomain.GetAssemblies()
            .Select(static candidate => candidate.GetType("Common.BenchmarkExecutionMode",throwOnError:false,ignoreCase:false))
            .FirstOrDefault(static candidate => candidate is not null);
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

static string CaptureBenchmarkResultDigest(Type benchmarkType,string methodName,object?[] arguments){
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

static int CreateRandomSourceSeed(WorkerTaskMessage task){
    var runRoot = TryGetRunRoot(task.ArtifactFolder);
    return StableHash(runRoot+"|"+task.SourceTab+"|"+task.MethodName+"|"+task.ArgumentKey+"|"+task.ElementCount.ToString(CultureInfo.InvariantCulture)+"|"+task.LoopCount.ToString(CultureInfo.InvariantCulture));
}

static bool IsReportForArguments(BenchmarkDotNet.Running.BenchmarkCase benchmarkCase,BenchmarkArgument[] arguments){
    if(arguments.Length==0){
        return true;
    }
    var display = benchmarkCase.DisplayInfo;
    return arguments.All(argument =>
        display.Contains(argument.Name,StringComparison.OrdinalIgnoreCase)
        &&display.Contains(argument.Value,StringComparison.OrdinalIgnoreCase));
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

static IReadOnlyList<BenchmarkDiagnosticCounter> CreateDiagnosticCounters(global::Seamless.Linq.LinqCounter counter) => [
    new("PhysicalMoveNext",counter.PhysicalMoveNext),
    new("LogicalOperations",counter.LogicalOperations),
    new("LogicalWhere",counter.LogicalWhere),
    new("LogicalSelect",counter.LogicalSelect),
    new("HashBuild",counter.HashBuild),
    new("HashProbe",counter.HashProbe),
    new("BufferAdd",counter.BufferAdd),
    new("TryGetNonEnumeratedCount",counter.TryGetNonEnumeratedCount),
    new("LogicalDelegate",counter.LogicalDelegate),
    new("PhysicalDelegate",counter.PhysicalDelegate),
    new("EnumerableFallback",counter.EnumerableFallback),
    new("DirectSpanUsed",counter.DirectSpanUsed),
    new("TryGetSpanCalled",counter.TryGetSpanCalled),
    new("TryGetSpanSucceeded",counter.TryGetSpanSucceeded),
    new("TryGetSpanFailed",counter.TryGetSpanFailed),
    new("EnumerationLoopUsed",counter.EnumerationLoopUsed),
    new("FastCopyUsed",counter.FastCopyUsed),
    new("HashCapacityHintUsed",counter.HashCapacityHintUsed),
];

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

static void ApplyThreadAffinity(int logicalCpu){
    if(logicalCpu<0||logicalCpu>=Environment.ProcessorCount||logicalCpu>=63||!OperatingSystem.IsWindows()){
        return;
    }

    var mask = 1UL<<logicalCpu;
    SetThreadAffinityMask(GetCurrentThread(),new UIntPtr(mask));
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
    string ArgumentKey,
    BenchmarkArgument[] Arguments,
    int SourceTab,
    int[] PreparedElementCounts,
    int ElementCount,
    int LoopCount,
    string ElementScale,
    string ArtifactFolder,
    int? AssignedLogicalCpu = null);

internal sealed record BenchmarkArgument(string Name,string Value);

internal static class BenchmarkArgumentFormatting{
    public static object?[] ToObjects(IReadOnlyList<BenchmarkArgument> arguments) =>
        arguments.Select(static argument => ParseValue(argument.Value)).ToArray();

    private static object ParseValue(string value) {
        if(long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var longValue)){
            return longValue;
        }
        if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var doubleValue)){
            return doubleValue;
        }
        return value;
    }
}

internal sealed record BenchmarkDiagnosticCounter(
    string CounterName,
    double CounterValue);

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
    string ResultDigest,
    IReadOnlyList<BenchmarkDiagnosticCounter>? DiagnosticCounters = null);
internal sealed class EmptyDisposable:IDisposable{
    public static EmptyDisposable Instance { get; } = new();

    public void Dispose(){
    }
}

internal sealed class WorkerTaskFilter(WorkerTaskMessage task) : IFilter{
    public bool Predicate(BenchmarkDotNet.Running.BenchmarkCase benchmarkCase) =>
        benchmarkCase.Descriptor.WorkloadMethod.Name==task.MethodName
        &&IsReportForArguments(benchmarkCase,task.Arguments);

    private static bool IsReportForArguments(BenchmarkDotNet.Running.BenchmarkCase benchmarkCase,BenchmarkArgument[] arguments){
        if(arguments.Length==0){
            return true;
        }
        var display = benchmarkCase.DisplayInfo;
        return arguments.All(argument =>
            display.Contains(argument.Name,StringComparison.OrdinalIgnoreCase)
            &&display.Contains(argument.Value,StringComparison.OrdinalIgnoreCase));
    }
}
