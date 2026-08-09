using System.Reflection;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.NativeAot;

// Worker.Aot自身はNativeAotToolchainが子プロセスとして生成・ビルドするAOT対象とは別物であり、
// 通常のフレームワーク依存プロセス(JIT)として動く。よってWorker.exeと同じくAssembly.Load+GetTypeの
// リフレクション解決で構わない(静的typeof()列挙は不要かつ、カタログ拡張のたびに保守が要る)。
if(args.Length==0||args[0] is "--help"){
    Console.WriteLine("usage: Worker.Aot <アセンブリ名> <型完全名> <出力ディレクトリ>");
    return args.Length==0 ? 1 : 0;
}

if(args.Length<3){
    Console.Error.WriteLine("usage: Worker.Aot <アセンブリ名> <型完全名> <出力ディレクトリ>");
    return 1;
}

var アセンブリ名=args[0];
var 型完全名=args[1];
var 出力ディレクトリ=args[2];

Assembly アセンブリ;
try{
    アセンブリ=Assembly.Load(new AssemblyName(アセンブリ名));
}catch(Exception 例外){
    Console.Error.WriteLine($"アセンブリを読み込めません: {アセンブリ名}: {例外.Message}");
    return 2;
}

var 対象型=アセンブリ.GetType(型完全名);
if(対象型 is null){
    Console.Error.WriteLine($"未知の型: {アセンブリ名}.{型完全名}");
    return 2;
}

Directory.CreateDirectory(出力ディレクトリ);

var 設定=ManualConfig.CreateEmpty()
    .AddJob(Job.Default.WithToolchain(NativeAotToolchain.Net10_0))
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddLogger(ConsoleLogger.Default)
    .AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance)
    .AddExporter(JsonExporter.Full)
    .WithArtifactsPath(出力ディレクトリ);

//未フィルタでその型の全[Benchmark]メソッドを1回のILCビルド+実行でまとめて測定する。
BenchmarkSwitcher.FromTypes([対象型]).Run(["--filter","*"],設定);
return 0;
