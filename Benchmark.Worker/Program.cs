using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

Console.OutputEncoding=Encoding.UTF8;
Console.InputEncoding=Encoding.UTF8;
const string ElementScaleEnvironmentVariable = "SEAMLESS_BENCHMARK_ELEMENT_SCALE";

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
            1,
            task?.ArtifactFolder??string.Empty,
            ex.ToString());
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
    Console.SetOut(logWriter);
    Console.SetError(logWriter);
    try{
        ApplyAffinity(logicalCpu);
        Environment.SetEnvironmentVariable(ElementScaleEnvironmentVariable,string.IsNullOrWhiteSpace(task.ElementScale)?"1":task.ElementScale);
        var benchmarkType = ResolveBenchmarkType(task.AssemblyName,task.TypeFullName);
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
            .WithArtifactsPath(task.ArtifactFolder);
        var summaries = BenchmarkSwitcher
            .FromTypes([benchmarkType])
            .Run(["--filter","*."+task.MethodName],config);
        var summary = summaries.FirstOrDefault();
        var report = summary?.Reports.FirstOrDefault();
        var stats = report?.ResultStatistics;
        var log = logWriter.ToString();
        File.WriteAllText(logPath,log,Encoding.UTF8);
        return new WorkerResultMessage(
            task.TaskId,
            "Completed",
            FormatNanoseconds(stats?.Mean),
            FormatNanoseconds(stats?.StandardError),
            FormatNanoseconds(stats?.StandardDeviation),
            0,
            task.ArtifactFolder,
            log);
    }finally{
        Environment.SetEnvironmentVariable(ElementScaleEnvironmentVariable,previousScale);
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

static string FormatNanoseconds(double? value){
    return value is { } number&&double.IsFinite(number)
        ? number.ToString("N3",CultureInfo.InvariantCulture)+" ns"
        : string.Empty;
}

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
    string ElementScale,
    string ArtifactFolder);

internal sealed record WorkerResultMessage(
    string TaskId,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    int ExitCode,
    string ArtifactFolder,
    string Log);
