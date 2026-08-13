using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using Benchmark.Aot.Protocol;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.NativeAot;

using Perfolizer.Horology;

namespace Worker.Aot;

internal static class AOTワーカーアプリ {
    private const string 要素倍率環境変数名="SEAMLESS_BENCHMARK_ELEMENT_SCALE";
    private const string 要素数環境変数名="SEAMLESS_BENCHMARK_ELEMENT_COUNT";
    private const string 準備済み要素数環境変数名="SEAMLESS_BENCHMARK_PREPARED_ELEMENT_COUNTS";
    private const string ランダムソースシード環境変数名="SEAMLESS_BENCHMARK_RANDOM_SOURCE_SEED";
    private const string 必須アフィニティCPU環境変数名="SEAMLESS_BENCHMARK_REQUIRED_AFFINITY_CPU";

    public static async Task<int> 実行Async(string[] 引数,CancellationToken キャンセルトークン) {
        if(引数.Length==0||引数[0] is "--help") {
            Console.WriteLine("usage: Worker.Aot --preflight --output <dir> | --request <request.json>");
            if(引数.Length==0) {
                return 1;
            }
            return 0;
        }
        if(string.Equals(引数[0],"--preflight",StringComparison.Ordinal)) {
            var 出力=オプション値を取得(引数,"--output");
            return await 事前検査Async(出力,キャンセルトークン);
        }
        if(string.Equals(引数[0],"--request",StringComparison.Ordinal)&&引数.Length==2) {
            return await 要求を実行Async(Path.GetFullPath(引数[1]),キャンセルトークン);
        }
        Console.Error.WriteLine("usage: Worker.Aot --preflight --output <dir> | --request <request.json>");
        return 1;
    }

    private static async Task<int> 事前検査Async(string 出力ディレクトリ,CancellationToken キャンセルトークン) {
        出力ディレクトリ=Path.GetFullPath(出力ディレクトリ);
        Directory.CreateDirectory(出力ディレクトリ);
        var ログパス=Path.Combine(出力ディレクトリ,"preflight.log");
        try {
            var 情報=await ツールチェーン情報を取得Async(ログパス,キャンセルトークン);
            var ソリューションルート=ソリューションルートを検索()
                ??throw new DirectoryNotFoundException("Seamless.slnxを検索できないためNativeAOT probeを実行できません。");
            var Probeプロジェクト=Path.Combine(ソリューションルート,"Benchmark","Aot.Probe","Aot.Probe.csproj");
            if(!File.Exists(Probeプロジェクト)) {
                throw new FileNotFoundException("NativeAOT probeプロジェクトが見つかりません。",Probeプロジェクト);
            }
            var Probe出力=Path.Combine(出力ディレクトリ,"probe");
            Directory.CreateDirectory(Probe出力);
            var 公開結果=await プロセスを実行Async(
                "dotnet",
                ["publish",Probeプロジェクト,"-c","Release","-r","win-x64","--self-contained","true","-o",Probe出力,"-m:1","-p:UseSharedCompilation=false"],
                ソリューションルート,
                ログパス,
                TimeSpan.FromMinutes(15),
                キャンセルトークン);
            if(公開結果!=0) {
                throw new InvalidOperationException($"NativeAOT probeのpublishに失敗しました(終了コード={公開結果})。Visual Studio C++ Desktop developmentとWindows SDKを確認してください。ログ: {ログパス}");
            }
            var Probe実行ファイル=Path.Combine(Probe出力,"Aot.Probe.exe");
            if(!File.Exists(Probe実行ファイル)) {
                throw new FileNotFoundException("NativeAOT probe実行ファイルが生成されませんでした。",Probe実行ファイル);
            }
            var 実行結果=await プロセスを実行Async(Probe実行ファイル,[],Probe出力,ログパス,TimeSpan.FromMinutes(1),キャンセルトークン);
            if(実行結果!=0) {
                throw new InvalidOperationException($"NativeAOT probeの起動に失敗しました(終了コード={実行結果})。ログ: {ログパス}");
            }
            var 応答=new AOT事前検査応答(
                AOT契約.現在バージョン,
                "Completed",
                情報.指紋,
                情報.DotnetSDK,
                情報.VisualStudioパス,
                情報.Linkerパス,
                情報.WindowsSDKパス,
                Probe実行ファイル,
                ログパス);
            JSONを原子的に保存(Path.Combine(出力ディレクトリ,AOT契約.事前検査応答ファイル名),応答);
            Console.WriteLine(JsonSerializer.Serialize(応答,AOT契約.JSON設定));
            return 0;
        } catch(Exception 例外) {
            var 応答=new AOT事前検査応答(AOT契約.現在バージョン,"Failed",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,ログパス,例外.Message);
            JSONを原子的に保存(Path.Combine(出力ディレクトリ,AOT契約.事前検査応答ファイル名),応答);
            Console.Error.WriteLine(例外);
            return 10;
        }
    }

    private static async Task<int> 要求を実行Async(string 要求パス,CancellationToken キャンセルトークン) {
        AOTワーカー要求? 要求=null;
        string 成果物ディレクトリ=Path.GetDirectoryName(要求パス)??Directory.GetCurrentDirectory();
        string ログパス=Path.Combine(成果物ディレクトリ,"aot-worker.log");
        try {
            await using(var ストリーム=File.OpenRead(要求パス)) {
                要求=await JsonSerializer.DeserializeAsync<AOTワーカー要求>(ストリーム,AOT契約.JSON設定,キャンセルトークン)
                    ??throw new InvalidDataException("AOT要求JSONが空です。");
            }
            AOT要求検証.検証(要求);
            成果物ディレクトリ=Path.GetFullPath(要求.成果物ディレクトリ);
            Directory.CreateDirectory(成果物ディレクトリ);
            ログパス=Path.Combine(成果物ディレクトリ,"aot-worker.log");
            var 情報=await ツールチェーン情報を取得Async(ログパス,キャンセルトークン);
            if(!string.Equals(情報.指紋,要求.ツールチェーン指紋,StringComparison.Ordinal)) {
                throw new InvalidOperationException("事前検査後にNativeAOTツールチェーンが変化しました。AOT事前検査をやり直してください。");
            }
            var 応答=AOT測定を実行(要求,情報,ログパス);
            JSONを原子的に保存(Path.Combine(成果物ディレクトリ,AOT契約.応答ファイル名),応答);
            JSONを原子的に保存(Path.Combine(成果物ディレクトリ,AOT契約.完了ファイル名),応答);
            Console.WriteLine(JsonSerializer.Serialize(応答,AOT契約.JSON設定));
            return 0;
        } catch(Exception 例外) {
            var 失敗理由=最初の実行可能な診断を取得(ログパス,例外.Message);
            var 応答=new AOTワーカー応答(
                AOT契約.現在バージョン,
                "Failed",
                false,
                要求?.論理CPU??-1,
                false,
                要求?.ツールチェーン指紋??string.Empty,
                要求?.要求指紋??string.Empty,
                [],
                成果物ディレクトリ,
                ログパス,
                失敗理由);
            Directory.CreateDirectory(成果物ディレクトリ);
            JSONを原子的に保存(Path.Combine(成果物ディレクトリ,AOT契約.応答ファイル名),応答);
            Console.Error.WriteLine(例外);
            return 20;
        }
    }

    private static AOTワーカー応答 AOT測定を実行(AOTワーカー要求 要求,AOTツールチェーン情報 情報,string ログパス) {
        if(!File.Exists(要求.アセンブリパス)) {
            throw new FileNotFoundException("AOT対象アセンブリが見つかりません。",要求.アセンブリパス);
        }
        var アセンブリ=Assembly.LoadFrom(要求.アセンブリパス);
        if(!string.Equals(アセンブリ.GetName().Name,要求.アセンブリ名,StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException($"AOT対象アセンブリ名が一致しません。要求={要求.アセンブリ名} 実体={アセンブリ.GetName().Name}");
        }
        var 対象型=アセンブリ.GetType(要求.型完全名,throwOnError:true,ignoreCase:false)!;
        要求メソッドを検証(対象型,要求.ケース一覧);
        var ソリューションルート=ソリューションルートを検索()
            ??throw new DirectoryNotFoundException("Seamless.slnxを検索できないためAOT正規プロジェクトを解決できません。");
        var 正規プロジェクトパス=AOT正規プロジェクト解決.解決(ソリューションルート,要求.アセンブリ名);
        var 要求キー一覧=要求.ケース一覧.Select(static ケース=>(ケース.メソッド名,正規化(ケース.引数キー))).ToHashSet();
        var Job設定=Job.Default
            .WithToolchain(NativeAotToolchain.Net10_0)
            .WithLaunchCount(要求.設定.起動回数)
            .WithWarmupCount(要求.設定.ウォームアップ回数)
            .WithIterationCount(要求.設定.実測回数)
            .WithIterationTime(TimeInterval.FromMilliseconds(要求.設定.実測時間ミリ秒))
            .WithEnvironmentVariable(必須アフィニティCPU環境変数名,要求.論理CPU.ToString(CultureInfo.InvariantCulture))
            .WithAffinity(new IntPtr(1L<<要求.論理CPU));
        var 設定=ManualConfig.CreateEmpty()
            .AddJob(Job設定)
            .AddFilter(new SimpleFilter(ケース=>要求キー一覧.Contains((ケース.Descriptor.WorkloadMethod.Name,正規化(ケース.Parameters.DisplayInfo)))))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddLogger(ConsoleLogger.Default)
            .AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance)
            .AddExporter(JsonExporter.Full)
            .WithArtifactsPath(要求.成果物ディレクトリ);

        var 要素数=要求.ケース一覧.Select(static ケース=>ケース.要素数).Distinct().Single();
        var 前回要素倍率=Environment.GetEnvironmentVariable(要素倍率環境変数名);
        var 前回要素数=Environment.GetEnvironmentVariable(要素数環境変数名);
        var 前回準備済み要素数=Environment.GetEnvironmentVariable(準備済み要素数環境変数名);
        var 前回ランダムソースシード=Environment.GetEnvironmentVariable(ランダムソースシード環境変数名);
        var シード=安定ハッシュ(要求.実行ID+"|"+要素数.ToString(CultureInfo.InvariantCulture));
        using var プロジェクト探索スコープ=BDNプロジェクト探索スコープ.作成(正規プロジェクトパス,要求.アセンブリ名,要求.要求指紋,情報.DotnetSDK);
        try {
            Environment.SetEnvironmentVariable(要素倍率環境変数名,"1");
            Environment.SetEnvironmentVariable(要素数環境変数名,要素数.ToString(CultureInfo.InvariantCulture));
            Environment.SetEnvironmentVariable(準備済み要素数環境変数名,要素数.ToString(CultureInfo.InvariantCulture));
            Environment.SetEnvironmentVariable(ランダムソースシード環境変数名,シード.ToString(CultureInfo.InvariantCulture));
            using var ログ書込=new StreamWriter(ログパス,append:true,new UTF8Encoding(false)) { AutoFlush=true };
            using var 出力差替=new コンソール差替スコープ(ログ書込);
            var 概要=BenchmarkRunner.Run(対象型,設定);
            if(!File.Exists(ログパス)||new FileInfo(ログパス).Length==0) {
                throw new FileNotFoundException("BenchmarkDotNetビルド・実行ログが生成されませんでした。",ログパス);
            }
            _=AOT完全JSONレポート検証.検証(要求.成果物ディレクトリ,情報.DotnetSDK,要求.ケース一覧);
            return 応答を作成(要求,対象型,概要,情報,ログパス,シード);
        } finally {
            Environment.SetEnvironmentVariable(要素倍率環境変数名,前回要素倍率);
            Environment.SetEnvironmentVariable(要素数環境変数名,前回要素数);
            Environment.SetEnvironmentVariable(準備済み要素数環境変数名,前回準備済み要素数);
            Environment.SetEnvironmentVariable(ランダムソースシード環境変数名,前回ランダムソースシード);
        }
    }

    private static AOTワーカー応答 応答を作成(AOTワーカー要求 要求,Type 対象型,Summary 概要,AOTツールチェーン情報 情報,string ログパス,int シード) {
        if(概要.HasCriticalValidationErrors) {
            throw new InvalidOperationException("BenchmarkDotNetが重大な検証エラーを報告しました。ログ: "+ログパス);
        }
        var 報告一覧=概要.Reports.ToArray();
        var 結果一覧=new List<AOTケース結果>(要求.ケース一覧.Count);
        foreach(var ケース in 要求.ケース一覧) {
            var 一致一覧=報告一覧.Where(報告=>
                string.Equals(報告.BenchmarkCase.Descriptor.WorkloadMethod.Name,ケース.メソッド名,StringComparison.Ordinal)
                &&string.Equals(正規化(報告.BenchmarkCase.Parameters.DisplayInfo),正規化(ケース.引数キー),StringComparison.Ordinal)).ToArray();
            if(一致一覧.Length!=1) {
                throw new InvalidDataException($"AOT結果件数が一致しません: {ケース.メソッド名}({ケース.引数キー}) 件数={一致一覧.Length}");
            }
            var 報告=一致一覧[0];
            if(!報告.Success||報告.ResultStatistics is not { } 統計) {
                throw new InvalidOperationException($"NativeAOT測定が失敗しました: {ケース.メソッド名}({ケース.引数キー})。ログ: {ログパス}");
            }
            if(!double.IsFinite(統計.Mean)||!double.IsFinite(統計.StandardError)||!double.IsFinite(統計.StandardDeviation)) {
                throw new InvalidDataException($"NativeAOT統計が有限値ではありません: {ケース.メソッド名}({ケース.引数キー})");
            }
            if(報告.BenchmarkCase.Job.Infrastructure.Toolchain is not NativeAotToolchain) {
                throw new InvalidDataException($"NativeAOT以外のtoolchain結果を拒否しました: {ケース.メソッド名}({ケース.引数キー})");
            }
            var ダイジェスト=結果ダイジェストを取得(対象型,ケース,シード);
            結果一覧.Add(new AOTケース結果(
                ケース.メソッド名,
                ケース.引数キー,
                "Completed",
                統計.Mean,
                統計.StandardError,
                統計.StandardDeviation,
                一操作割当量を取得(報告),
                ダイジェスト));
        }
        if(報告一覧.Length!=要求.ケース一覧.Count) {
            throw new InvalidDataException($"未選択のBenchmarkが実行されました。要求={要求.ケース一覧.Count} 結果={報告一覧.Length}");
        }
        return new AOTワーカー応答(
            AOT契約.現在バージョン,
            "Completed",
            true,
            要求.論理CPU,
            true,
            情報.指紋,
            要求.要求指紋,
            結果一覧,
            要求.成果物ディレクトリ,
            ログパス);
    }

    private static long 一操作割当量を取得(BenchmarkReport 報告) {
        var GC統計=(object)報告.GcStats;
        var 型=GC統計.GetType();
        var 総操作回数=型.GetProperty("TotalOperations")?.GetValue(GC統計) as long?;
        var 合計取得メソッド=型.GetMethod("GetTotalAllocatedBytes",[typeof(bool)]);
        var 合計一覧=new object?[] {
            合計取得メソッド?.Invoke(GC統計,[false]),
            型.GetProperty("AllocatedBytes")?.GetValue(GC統計),
            合計取得メソッド?.Invoke(GC統計,[true]),
        };
        long? 総割当量=null;
        foreach(var 値 in 合計一覧) {
            if(値 is long 数値&&数値>=0) {
                総割当量=数値;
                if(数値>0) {
                    break;
                }
            }
        }
        if(総割当量 is null||総操作回数 is null||総操作回数<=0) {
            throw new InvalidDataException("BenchmarkDotNetの割当量または操作回数を取得できませんでした。");
        }
        return checked((long)Math.Round((double)総割当量.Value/総操作回数.Value));
    }

    private static string 結果ダイジェストを取得(Type 対象型,AOTケース要求 ケース,int シード) {
        var メソッド=対象型.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)
            .Single(候補=>string.Equals(候補.Name,ケース.メソッド名,StringComparison.Ordinal)&&ベンチマーク属性を持つか(候補));
        var 引数=メソッド.GetParameters().Select((引数情報,インデックス)=>値を変換(ケース.引数一覧[インデックス].値,引数情報.ParameterType)).ToArray();
        using var 要素数スコープ=スコープを開始(対象型.Assembly,"UseElementCount",[ケース.要素数,"1"],[typeof(int),typeof(string)]);
        using var シードスコープ=スコープを開始(対象型.Assembly,"UseRandomSourceSeed",[シード],[typeof(int)]);
        var インスタンス=Activator.CreateInstance(対象型);
        try {
            var 取得メソッド=対象型.GetMethod("CaptureBenchmarkMethodResult",BindingFlags.Public|BindingFlags.Instance,null,[typeof(string),typeof(bool),typeof(object[])],null)
                ??throw new MissingMethodException(対象型.FullName,"CaptureBenchmarkMethodResult");
            return 取得メソッド.Invoke(インスタンス,[ケース.メソッド名,false,引数]) as string
                ??throw new InvalidOperationException("AOT結果ダイジェストが空でした。");
        } finally {
            if(インスタンス is IDisposable 破棄対象) {
                破棄対象.Dispose();
            }
        }
    }

    private static void 要求メソッドを検証(Type 対象型,IReadOnlyList<AOTケース要求> ケース一覧) {
        foreach(var ケース in ケース一覧) {
            var 候補一覧=対象型.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)
                .Where(候補=>string.Equals(候補.Name,ケース.メソッド名,StringComparison.Ordinal)&&ベンチマーク属性を持つか(候補)).ToArray();
            if(候補一覧.Length!=1) {
                throw new MissingMethodException(対象型.FullName,ケース.メソッド名);
            }
            if(候補一覧[0].GetParameters().Length!=ケース.引数一覧.Count) {
                throw new InvalidDataException($"AOTケースの引数数が一致しません: {ケース.メソッド名}");
            }
        }
    }

    private static bool ベンチマーク属性を持つか(MethodInfo メソッド) =>
        メソッド.GetCustomAttributes(inherit:true).Any(static 属性=>属性.GetType().Name=="BenchmarkAttribute");

    private static object? 値を変換(string 値,Type 型) {
        if(型==typeof(string)) {
            return 値;
        }
        if(型.IsEnum) {
            return Enum.Parse(型,値,ignoreCase:true);
        }
        return Convert.ChangeType(値,型,CultureInfo.InvariantCulture);
    }

    private static IDisposable スコープを開始(Assembly アセンブリ,string メソッド名,object?[] 引数,Type[] 引数型一覧) {
        var 型=アセンブリ.GetType("Common.BenchmarkExecutionMode",false,false)
            ??AppDomain.CurrentDomain.GetAssemblies().Select(static 候補=>候補.GetType("Common.BenchmarkExecutionMode",false,false)).FirstOrDefault(static 候補=>候補 is not null);
        return 型?.GetMethod(メソッド名,BindingFlags.Public|BindingFlags.Static,null,引数型一覧,null)?.Invoke(null,引数) as IDisposable
            ??空ディスポーザブル.インスタンス;
    }

    private static async Task<AOTツールチェーン情報> ツールチェーン情報を取得Async(string ログパス,CancellationToken キャンセルトークン) {
        if(!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException("NativeAOTベンチマークはWindows x64専用です。");
        }
        var Dotnet結果=await プロセス出力を取得Async("dotnet",["--version"],Directory.GetCurrentDirectory(),ログパス,TimeSpan.FromMinutes(1),キャンセルトークン);
        if(Dotnet結果.終了コード!=0||!Dotnet結果.標準出力.TrimStart().StartsWith("10.",StringComparison.Ordinal)) {
            throw new InvalidOperationException(".NET 10 SDKを確認できませんでした。ログ: "+ログパス);
        }
        var ProgramFilesX86=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var Vswhereパス=Path.Combine(ProgramFilesX86,"Microsoft Visual Studio","Installer","vswhere.exe");
        if(!File.Exists(Vswhereパス)) {
            throw new FileNotFoundException("Visual Studio Installerのvswhere.exeが見つかりません。",Vswhereパス);
        }
        var VS結果=await プロセス出力を取得Async(Vswhereパス,["-latest","-products","*","-requires","Microsoft.VisualStudio.Component.VC.Tools.x86.x64","-property","installationPath"],Directory.GetCurrentDirectory(),ログパス,TimeSpan.FromMinutes(1),キャンセルトークン);
        var VisualStudioパス=VS結果.標準出力.Trim();
        if(VS結果.終了コード!=0||string.IsNullOrWhiteSpace(VisualStudioパス)||!Directory.Exists(VisualStudioパス)) {
            throw new DirectoryNotFoundException("Visual Studio C++ Desktop development workloadを確認できませんでした。");
        }
        var Linkerパス=Directory.EnumerateFiles(Path.Combine(VisualStudioパス,"VC","Tools","MSVC"),"link.exe",SearchOption.AllDirectories)
            .Where(static パス=>パス.Contains($"{Path.DirectorySeparatorChar}Hostx64{Path.DirectorySeparatorChar}x64{Path.DirectorySeparatorChar}",StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static パス=>パス,StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ??throw new FileNotFoundException("x64 link.exeが見つかりません。Visual Studio C++ toolsetを修復してください。");
        var WindowsSDKルート=Path.Combine(ProgramFilesX86,"Windows Kits","10","Lib");
        string? WindowsSDKパス=null;
        if(Directory.Exists(WindowsSDKルート)) {
            WindowsSDKパス=Directory.EnumerateDirectories(WindowsSDKルート)
                .Where(パス=>File.Exists(Path.Combine(パス,"um","x64","kernel32.lib")))
                .OrderByDescending(static パス=>パス,StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        if(WindowsSDKパス is null) {
            throw new DirectoryNotFoundException("Windows SDK x64 libraryを確認できませんでした。");
        }
        var 指紋=AOT契約.指紋を作成(string.Join("|",Dotnet結果.標準出力.Trim(),VisualStudioパス,File.GetLastWriteTimeUtc(Linkerパス).Ticks,WindowsSDKパス));
        return new(Dotnet結果.標準出力.Trim(),VisualStudioパス,Linkerパス,WindowsSDKパス,指紋);
    }

    private static async Task<int> プロセスを実行Async(string 実行ファイル,IReadOnlyList<string> 引数,string 作業ディレクトリ,string ログパス,TimeSpan タイムアウト,CancellationToken キャンセルトークン) {
        var 結果=await プロセス出力を取得Async(実行ファイル,引数,作業ディレクトリ,ログパス,タイムアウト,キャンセルトークン);
        return 結果.終了コード;
    }

    private static async Task<プロセス結果> プロセス出力を取得Async(string 実行ファイル,IReadOnlyList<string> 引数,string 作業ディレクトリ,string ログパス,TimeSpan タイムアウト,CancellationToken キャンセルトークン) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ログパス))!);
        var 開始情報=new ProcessStartInfo(実行ファイル) {
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            CreateNoWindow=true,
            WorkingDirectory=作業ディレクトリ,
            StandardOutputEncoding=Encoding.UTF8,
            StandardErrorEncoding=Encoding.UTF8,
        };
        foreach(var 値 in 引数) {
            開始情報.ArgumentList.Add(値);
        }
        using var プロセス=Process.Start(開始情報)??throw new InvalidOperationException("プロセスを開始できません: "+実行ファイル);
        var 標準出力タスク=プロセス.StandardOutput.ReadToEndAsync();
        var 標準エラータスク=プロセス.StandardError.ReadToEndAsync();
        using var タイムアウトソース=new CancellationTokenSource(タイムアウト);
        using var 連結ソース=CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
        var 中断したか=false;
        var タイムアウトしたか=false;
        try {
            await プロセス.WaitForExitAsync(連結ソース.Token);
        } catch(OperationCanceledException) {
            中断したか=true;
            タイムアウトしたか=タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested;
            try {
                プロセス.Kill(entireProcessTree:true);
            } catch {
            }
            await プロセス.WaitForExitAsync(CancellationToken.None);
        }
        var 標準出力=await 標準出力タスク;
        var 標準エラー=await 標準エラータスク;
        await File.AppendAllTextAsync(ログパス,$"$ {実行ファイル} {string.Join(' ',引数)}{Environment.NewLine}{標準出力}{標準エラー}{Environment.NewLine}",Encoding.UTF8,CancellationToken.None);
        if(タイムアウトしたか) {
            throw new TimeoutException($"プロセスがタイムアウトしました: {実行ファイル} ({タイムアウト})");
        }
        if(中断したか) {
            キャンセルトークン.ThrowIfCancellationRequested();
            throw new OperationCanceledException();
        }
        return new(プロセス.ExitCode,標準出力,標準エラー);
    }

    private static string オプション値を取得(string[] 引数,string 名前) {
        var インデックス=Array.FindIndex(引数,値=>string.Equals(値,名前,StringComparison.Ordinal));
        if(インデックス<0||インデックス+1>=引数.Length||string.IsNullOrWhiteSpace(引数[インデックス+1])) {
            throw new ArgumentException($"{名前}が必要です。");
        }
        return 引数[インデックス+1];
    }

    private static string 正規化(string 値) => string.Join(",",値.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries));

    private static string 最初の実行可能な診断を取得(string ログパス,string 代替診断) {
        if(!File.Exists(ログパス)) {
            return 代替診断;
        }
        try {
            var 行一覧=File.ReadLines(ログパス,Encoding.UTF8)
                .Select(static 行=>行.Trim())
                .Where(static 行=>行.Length>0)
                .ToArray();
            var パターン一覧=new[] {
                "Found more than one matching project file",
                "Unable to find",
                "link.exe",
                "Visual C++",
                ": error ",
                " error MSB",
                "Exception:",
            };
            foreach(var パターン in パターン一覧) {
                var 行=行一覧.FirstOrDefault(候補=>候補.Contains(パターン,StringComparison.OrdinalIgnoreCase));
                if(行 is null) {
                    continue;
                }
                if(行.Length>1000) {
                    return 行[..1000];
                }
                return 行;
            }
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
        return 代替診断;
    }

    private static string? ソリューションルートを検索() {
        foreach(var 開始 in new[] { Directory.GetCurrentDirectory(),AppContext.BaseDirectory }) {
            var ディレクトリ=new DirectoryInfo(開始);
            while(ディレクトリ is not null) {
                if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
                    return ディレクトリ.FullName;
                }
                ディレクトリ=ディレクトリ.Parent;
            }
        }
        return null;
    }

    private static void JSONを原子的に保存<T>(string パス,T 値) {
        var 一時パス=パス+".tmp-"+Guid.NewGuid().ToString("N");
        File.WriteAllText(一時パス,JsonSerializer.Serialize(値,AOT契約.JSON設定),new UTF8Encoding(false));
        File.Move(一時パス,パス,overwrite:true);
    }

    private static int 安定ハッシュ(string 値) {
        var ハッシュ=SHA256.HashData(Encoding.UTF8.GetBytes(値));
        return BitConverter.ToInt32(ハッシュ,0)&int.MaxValue;
    }

    private sealed record AOTツールチェーン情報(string DotnetSDK,string VisualStudioパス,string Linkerパス,string WindowsSDKパス,string 指紋);

    private sealed class BDNプロジェクト探索スコープ:IDisposable {
        private readonly string 元の作業ディレクトリ;
        private readonly string 一時ディレクトリ;
        private bool 破棄済み;

        private BDNプロジェクト探索スコープ(string 元の作業ディレクトリ,string 一時ディレクトリ) {
            this.元の作業ディレクトリ=元の作業ディレクトリ;
            this.一時ディレクトリ=一時ディレクトリ;
        }

        public static BDNプロジェクト探索スコープ 作成(string 正規プロジェクトパス,string アセンブリ名,string 要求指紋,string DotnetSDK) {
            var 一時ルート=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"Seamless.NativeAot.ProjectSearch"));
            var 指紋長=Math.Min(16,要求指紋.Length);
            var 一時ディレクトリ=Path.Combine(一時ルート,要求指紋[..指紋長]+"-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(一時ディレクトリ);
            var 隔離プロジェクトパス=Path.Combine(一時ディレクトリ,アセンブリ名+".csproj");
            隔離プロジェクトを作成(正規プロジェクトパス,隔離プロジェクトパス);
            File.WriteAllText(Path.Combine(一時ディレクトリ,"AotProjectIsolation.slnx"),"<Solution />",new UTF8Encoding(false));
            var SDK固定設定=new {
                sdk=new {
                    version=DotnetSDK,
                    rollForward="disable",
                    allowPrerelease=false,
                },
            };
            File.WriteAllText(
                Path.Combine(一時ディレクトリ,"global.json"),
                JsonSerializer.Serialize(SDK固定設定,AOT契約.JSON設定),
                new UTF8Encoding(false));
            var 元の作業ディレクトリ=Directory.GetCurrentDirectory();
            try {
                Directory.SetCurrentDirectory(一時ディレクトリ);
                return new BDNプロジェクト探索スコープ(元の作業ディレクトリ,一時ディレクトリ);
            } catch {
                安全に削除(一時ルート,一時ディレクトリ);
                throw;
            }
        }

        private static void 隔離プロジェクトを作成(string 正規プロジェクトパス,string 出力パス) {
            var 正規ディレクトリ=Path.GetDirectoryName(Path.GetFullPath(正規プロジェクトパス))
                ??throw new DirectoryNotFoundException(正規プロジェクトパス);
            var 文書=XDocument.Load(正規プロジェクトパス,LoadOptions.PreserveWhitespace);
            var ルート=文書.Root??throw new InvalidDataException("AOT正規プロジェクトXMLにルートがありません。");
            foreach(var 要素 in ルート.Descendants()) {
                XAttribute? パス属性=null;
                if(string.Equals(要素.Name.LocalName,"ProjectReference",StringComparison.Ordinal)) {
                    パス属性=要素.Attribute("Include");
                } else if(string.Equals(要素.Name.LocalName,"Import",StringComparison.Ordinal)) {
                    パス属性=要素.Attribute("Project");
                }
                if(パス属性 is null||Path.IsPathRooted(パス属性.Value)) {
                    continue;
                }
                if(パス属性.Value.Contains("$(",StringComparison.Ordinal)) {
                    throw new InvalidDataException("AOT隔離プロジェクトで展開できないMSBuildパスです: "+パス属性.Value);
                }
                パス属性.Value=Path.GetFullPath(Path.Combine(正規ディレクトリ,パス属性.Value));
            }

            var 名前空間=ルート.Name.Namespace;
            ルート.AddFirst(
                new XElement(名前空間+"PropertyGroup",
                    new XElement(名前空間+"EnableDefaultCompileItems","false"),
                    new XElement(名前空間+"ImportDirectoryBuildProps","false"),
                    new XElement(名前空間+"ImportDirectoryBuildTargets","false")));
            var ソース一覧=Directory.EnumerateFiles(正規ディレクトリ,"*.cs",SearchOption.AllDirectories)
                .Where(パス=>ローカルソースか(正規ディレクトリ,パス))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if(ソース一覧.Length>0) {
                var 項目グループ=new XElement(名前空間+"ItemGroup");
                foreach(var ソース in ソース一覧) {
                    項目グループ.Add(new XElement(名前空間+"Compile",new XAttribute("Include",Path.GetFullPath(ソース))));
                }
                ルート.Add(項目グループ);
            }
            文書.Save(出力パス);
        }

        private static bool ローカルソースか(string プロジェクトディレクトリ,string パス) {
            var 相対パス=Path.GetRelativePath(プロジェクトディレクトリ,パス);
            var 先頭=相対パス.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)[0];
            return !先頭.Equals("bin",StringComparison.OrdinalIgnoreCase)
                &&!先頭.Equals("obj",StringComparison.OrdinalIgnoreCase)
                &&!先頭.Equals("Artifacts",StringComparison.OrdinalIgnoreCase)
                &&!先頭.Equals("Work",StringComparison.OrdinalIgnoreCase)
                &&!先頭.StartsWith("bin-",StringComparison.OrdinalIgnoreCase)
                &&!先頭.Equals("generated-inspection",StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose() {
            if(this.破棄済み) {
                return;
            }
            this.破棄済み=true;
            Directory.SetCurrentDirectory(this.元の作業ディレクトリ);
            var 一時ルート=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"Seamless.NativeAot.ProjectSearch"));
            安全に削除(一時ルート,this.一時ディレクトリ);
        }

        private static void 安全に削除(string 一時ルート,string 対象) {
            var 正規ルート=Path.GetFullPath(一時ルート)+Path.DirectorySeparatorChar;
            var 正規対象=Path.GetFullPath(対象);
            if(!正規対象.StartsWith(正規ルート,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(正規対象)) {
                return;
            }
            try {
                Directory.Delete(正規対象,recursive:true);
            } catch(IOException) {
            } catch(UnauthorizedAccessException) {
            }
        }
    }
    private sealed record プロセス結果(int 終了コード,string 標準出力,string 標準エラー);

    private sealed class 空ディスポーザブル:IDisposable {
        public static 空ディスポーザブル インスタンス{get;}=new();
        public void Dispose() {
        }
    }

    private sealed class コンソール差替スコープ:IDisposable {
        private readonly TextWriter 元出力=Console.Out;
        private readonly TextWriter 元エラー=Console.Error;

        public コンソール差替スコープ(TextWriter ログ) {
            Console.SetOut(new 複製TextWriter(this.元出力,ログ));
            Console.SetError(new 複製TextWriter(this.元エラー,ログ));
        }

        public void Dispose() {
            Console.SetOut(this.元出力);
            Console.SetError(this.元エラー);
        }
    }

    private sealed class 複製TextWriter(TextWriter 左,TextWriter 右):TextWriter {
        public override Encoding Encoding=>左.Encoding;
        public override void Write(char 値) {
            左.Write(値);
            右.Write(値);
        }
        public override void WriteLine(string? 値) {
            左.WriteLine(値);
            右.WriteLine(値);
        }
        public override void Flush() {
            左.Flush();
            右.Flush();
        }
    }
}
