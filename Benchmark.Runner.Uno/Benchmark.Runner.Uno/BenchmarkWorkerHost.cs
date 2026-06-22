using global::System.Diagnostics;
using global::System.Globalization;
using global::System.Reflection;
using global::System.Runtime.InteropServices;
using global::System.Text;
using global::System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Benchmark.Runner.Uno;

internal static class BenchmarkWorkerHost{
    private const string WorkerModeArgument = "--worker";

    public static bool IsWorkerMode(string[] args){
        return args.Any(static arg => string.Equals(arg,WorkerModeArgument,StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<int> RunAsync(string[] args){
        Console.OutputEncoding=Encoding.UTF8;
        Console.InputEncoding=Encoding.UTF8;

        var logicalCpu = ReadRequiredIntArg(args,"--logical-cpu");
        var runRoot = ReadRequiredStringArg(args,"--run-root");
        Directory.CreateDirectory(runRoot);
        ApplyAffinity(logicalCpu);

        var protocolOutput = Console.Out;
        var protocolError = Console.Error;
        while(await Console.In.ReadLineAsync() is { } line){
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
    }

    private static WorkerResultMessage RunBenchmark(
        WorkerTaskMessage task,
        int logicalCpu,
        TextWriter protocolOutput,
        TextWriter protocolError){
        Directory.CreateDirectory(task.ArtifactFolder);
        var logPath = Path.Combine(task.ArtifactFolder,"worker.log");
        if(IsDiagnosticBenchmark(task)){
            const string log = "Diagnostic fast path completed.";
            File.WriteAllText(logPath,log,Encoding.UTF8);
            return new WorkerResultMessage(
                task.TaskId,
                "Completed",
                "0.000 ns",
                "0.000 ns",
                "0.000 ns",
                0,
                task.ArtifactFolder,
                log);
        }

        using var logWriter = new StringWriter(CultureInfo.InvariantCulture);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(logWriter);
        Console.SetError(logWriter);
        try{
            ApplyAffinity(logicalCpu);
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
            Console.SetOut(protocolOutput);
            Console.SetError(protocolError);
            originalOut.Flush();
            originalError.Flush();
        }
    }

    private static bool IsDiagnosticBenchmark(WorkerTaskMessage task){
        return string.Equals(task.MethodName,"Diagnostic_Instant",StringComparison.Ordinal);
    }

    private static Type ResolveBenchmarkType(string assemblyName,string typeFullName){
        var assembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(item => string.Equals(item.GetName().Name,assemblyName,StringComparison.OrdinalIgnoreCase))
            ?? Assembly.Load(new AssemblyName(assemblyName));
        return assembly.GetType(typeFullName,throwOnError:true,ignoreCase:false)
            ??throw new InvalidOperationException($"Benchmark type was not found: {typeFullName}");
    }

    private static string FormatNanoseconds(double? value){
        return value is { } number&&double.IsFinite(number)
            ? number.ToString("N3",CultureInfo.InvariantCulture)+" ns"
            : string.Empty;
    }

    private static int ReadRequiredIntArg(string[] args,string name){
        var value = ReadRequiredStringArg(args,name);
        return int.Parse(value,CultureInfo.InvariantCulture);
    }

    private static string ReadRequiredStringArg(string[] args,string name){
        for(var i=0;i<args.Length-1;i++){
            if(string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase)){
                return args[i+1];
            }
        }

        throw new ArgumentException($"Missing required argument: {name}");
    }

    private static void ApplyAffinity(int logicalCpu){
        if(logicalCpu<0||logicalCpu>=Environment.ProcessorCount||logicalCpu>=63){
            return;
        }

        var mask = 1L<<logicalCpu;
        if(OperatingSystem.IsWindows()){
            try{
                Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(mask);
            }catch(PlatformNotSupportedException){
            }catch(InvalidOperationException){
            }catch(global::System.ComponentModel.Win32Exception){
            }

            SetThreadAffinityMask(GetCurrentThread(),new UIntPtr((ulong)mask));
        }else if(OperatingSystem.IsLinux()){
            try{
                Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(mask);
            }catch(PlatformNotSupportedException){
            }catch(InvalidOperationException){
            }catch(global::System.ComponentModel.Win32Exception){
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr threadHandle,UIntPtr threadAffinityMask);
}
