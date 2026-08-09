using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.NativeAot;

// 文字列(カテゴリ名)→静的な型、という対応表。Assembly.Load等の実行時リフレクションを使わず、
// 全てコンパイル時に確定した型参照のみで構成しているためNative AOT対応。
var categoryTypes = new Dictionary<string, Type[]> {
    ["Array"] = [
        typeof(Benchmark.System.Linq.Enumerable.FLinq.FLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.ZLinq.ZLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.FLinq.FLinqClassInt32),
        typeof(Benchmark.System.Linq.Enumerable.ZLinq.ZLinqClassInt32),
    ],
    ["List"] = [
        typeof(Benchmark.System.Linq.Enumerable.List.FLinq.FLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.List.ZLinq.ZLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.List.FLinq.FLinqClassInt32),
        typeof(Benchmark.System.Linq.Enumerable.List.ZLinq.ZLinqClassInt32),
    ],
    ["IEnumerable"] = [
        typeof(Benchmark.System.Linq.Enumerable.IEnumerable.FLinq.FLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq.ZLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.IEnumerable.FLinq.FLinqClassInt32),
        typeof(Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq.ZLinqClassInt32),
    ],
    ["Range"] = [
        typeof(Benchmark.System.Linq.Enumerable.Range.FLinq.FLinqInt32),
        typeof(Benchmark.System.Linq.Enumerable.Range.ZLinq.ZLinqInt32),
    ],
};

if(args.Length == 0 || !categoryTypes.TryGetValue(args[0], out var types)) {
    Console.WriteLine("usage: AotBenchPoc <category> [methodFilterGlob...]");
    Console.WriteLine($"  category: {string.Join(", ", categoryTypes.Keys)}");
    return 1;
}

var resultsDir = Path.Combine(AppContext.BaseDirectory, "aot-results", args[0]);
Directory.CreateDirectory(resultsDir);

var config = ManualConfig.CreateEmpty()
    .AddJob(Job.Default.WithToolchain(NativeAotToolchain.Net10_0))
    .AddLogger(ConsoleLogger.Default)
    .AddColumnProvider(DefaultColumnProviders.Instance)
    .AddExporter(JsonExporter.Full)
    .WithArtifactsPath(resultsDir);

// 引数[1..]があればメソッド名フィルタ、無ければそのカテゴリの全メソッドを測定する。
var runArgs = args.Length > 1
    ? new[] { "--filter" }.Concat(args.Skip(1).Select(m => $"*.{m}")).ToArray()
    : ["--filter", "*"];

BenchmarkSwitcher.FromTypes(types).Run(runArgs, config);
return 0;
