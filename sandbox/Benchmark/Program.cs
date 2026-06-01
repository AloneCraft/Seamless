#pragma warning disable

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Kokuban;

namespace Benchmark;

internal static class Program
{
    public static int Main(string[] args)
    {
#if DEBUG
        Console.WriteLine(Chalk.Yellow["Benchmark is executed with DEBUG configuration. "]);
        Console.WriteLine();
        // Allow local runs in Debug as well; benchmark reliability is still best in Release.
#endif

        if (args.Length != 0)
        {
            Console.WriteLine($"Start Seamless benchmarks with args: {string.Join(' ', args)}");
            Console.WriteLine();
        }

        try
        {
            var globalConfig = GetCustomBenchmarkConfig(args ?? []);

            var switcher = BenchmarkSwitcher.FromAssemblies([typeof(Program).Assembly]);
            var summaries = switcher.Run(args, globalConfig).ToArray();

            if (summaries.Length == 0)
            {
                if (IsShowInfoMode(args))
                    return 0;

                Console.WriteLine();
                Console.WriteLine(Chalk.Red["Benchmark is not executed. Verify benchmark log file."]);
                return 1;
            }

            return 0;
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine();
            Console.WriteLine(Chalk.Red[ex.Message]);
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(Chalk.Red[ex.ToString()]);
            return 1;
        }
    }

    private static IConfig GetCustomBenchmarkConfig(string[] args)
    {
        // Split args at "--": everything before is BenchmarkDotNet args, after is extra args
        int sepIdx = Array.IndexOf(args, "--");
        string[] benchmarkArgs = sepIdx >= 0 ? args[..sepIdx] : args;
        string[] extraArgs = sepIdx >= 0 ? args[(sepIdx + 1)..] : [];

        string key = extraArgs.Length > 0 ? extraArgs[0] : "Default";
        ManualConfig config = key switch
        {
            "Default" => new DefaultBenchmarkConfig(),
            "InProcess" => new InProcessBenchmarkConfig(),
            "InProcessMonitoring" or "Test" => new InProcessMonitoringBenchmarkConfig(),
            "TargetFrameworks" => new TargetFrameworksBenchmarkConfig(),
            "ColdStart" => new ColdStartBenchmarkConfig(),
            "SystemLinq" => new SystemLinqBenchmarkConfig(),
            _ => throw new ArgumentException($"Specified benchmark config key is not supported: {key}"),
        };

        if (IsBenchmarkSelectMode(benchmarkArgs) || IsShowInfoMode(benchmarkArgs) || Array.IndexOf(extraArgs, "--verbose") >= 0)
            config.AddLogger(ConsoleLogger.Default);

        Console.WriteLine($"Run Benchmarks with config: {config.GetType().Name}");
        return config;
    }

    private static bool IsBenchmarkSelectMode(string[] args)
        => !args.AsSpan().Contains("--filter");

    private static bool IsShowInfoMode(string[] args)
        => args.AsSpan().ContainsAny(["--help", "--list", "--info", "--version"]);
}
