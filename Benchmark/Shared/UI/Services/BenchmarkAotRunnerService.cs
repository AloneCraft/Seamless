using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Benchmark.Aot.Protocol;

namespace UI;

public sealed class ベンチマークAOTワーカープロセスサービス:Iベンチマークジョブ実行器,IAsyncDisposable {
    private readonly IReadOnlyList<ベンチマークケース> 選択ケース一覧;
    private readonly SemaphoreSlim 実行ゲート=new(1,1);
    private readonly ConcurrentDictionary<string,Lazy<Task<IReadOnlyDictionary<(string メソッド名,string 引数キー),AOTケース結果>>>> グループ別結果一覧=new(StringComparer.Ordinal);
    private Lazy<Task<AOT事前検査応答>>? 事前検査タスク;

    public ベンチマークAOTワーカープロセスサービス(IReadOnlyList<ベンチマークケース> 選択ケース一覧) {
        ArgumentNullException.ThrowIfNull(選択ケース一覧);
        this.選択ケース一覧=選択ケース一覧.ToArray();
    }

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) {
        var 結果一覧=await this.グループ結果を取得Async(ジョブ,実行ルート,論理CPU,キャンセルトークン);
        if(!結果一覧.TryGetValue((ジョブ.メソッド名,ジョブ.引数キー),out var 結果)) {
            return 失敗結果を作成(ジョブ,論理CPU,実行ルート,$"選択したAOT結果がありません: {ジョブ.型完全名}.{ジョブ.メソッド名}({ジョブ.引数キー})");
        }
        if(!string.Equals(結果.状態,"Completed",StringComparison.Ordinal)
            ||結果.平均ナノ秒 is not { } 平均
            ||結果.誤差ナノ秒 is not { } 誤差
            ||結果.標準偏差ナノ秒 is not { } 標準偏差
            ||結果.割り当てバイト数 is not { } 割り当て
            ||!double.IsFinite(平均)
            ||!double.IsFinite(誤差)
            ||!double.IsFinite(標準偏差)
            ||string.IsNullOrWhiteSpace(結果.結果ダイジェスト)) {
            var 失敗理由=結果.失敗理由;
            if(string.IsNullOrWhiteSpace(失敗理由)) {
                失敗理由="NativeAOT結果が不完全です。";
            }
            return 失敗結果を作成(ジョブ,論理CPU,実行ルート,失敗理由);
        }
        return new ベンチマークジョブ実行結果(
            ジョブ.プロジェクト,
            ジョブ.メソッド名,
            ジョブ.型完全名,
            ジョブ.要素数,
            "Completed",
            ナノ秒を整形(平均),
            ナノ秒を整形(誤差),
            ナノ秒を整形(標準偏差),
            平均,
            誤差,
            標準偏差,
            割り当て,
            0,
            実行ルート,
            string.Empty,
            引数キー:ジョブ.引数キー,
            引数値一覧:ジョブ.引数一覧,
            ループ回数:1,
            結果ダイジェスト:結果.結果ダイジェスト,

            アフィニティ適用成功か:true,
            実行項目インデックス:ジョブ.実行項目インデックス);
    }

    public async ValueTask DisposeAsync() {
        await this.実行ゲート.WaitAsync();
        this.実行ゲート.Release();
        this.実行ゲート.Dispose();
    }

    private Task<IReadOnlyDictionary<(string,string),AOTケース結果>> グループ結果を取得Async(
        ベンチマークジョブ ジョブ,string 実行ルート,int 論理CPU,CancellationToken キャンセルトークン) {
        var キー=string.Join("|",ジョブ.アセンブリ名,ジョブ.型完全名,ジョブ.要素数,論理CPU);
        var 遅延=this.グループ別結果一覧.GetOrAdd(
            キー,
            _=>new Lazy<Task<IReadOnlyDictionary<(string,string),AOTケース結果>>>(
                ()=>this.グループを測定Async(ジョブ,実行ルート,論理CPU,キャンセルトークン),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return this.失敗時に除去Async(キー,遅延.Value);
    }

    private async Task<IReadOnlyDictionary<(string,string),AOTケース結果>> 失敗時に除去Async(
        string キー,Task<IReadOnlyDictionary<(string,string),AOTケース結果>> タスク) {
        try {
            return await タスク;
        } catch {
            this.グループ別結果一覧.TryRemove(キー,out _);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<(string,string),AOTケース結果>> グループを測定Async(
        ベンチマークジョブ ジョブ,string 実行ルート,int 論理CPU,CancellationToken キャンセルトークン) {
        await this.実行ゲート.WaitAsync(キャンセルトークン);
        try {
            var 事前検査=await this.事前検査を取得Async(実行ルート,キャンセルトークン);
            if(!string.Equals(事前検査.状態,"Completed",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(事前検査.ツールチェーン指紋)) {
                throw new InvalidOperationException("NativeAOT事前検査に失敗しました: "+事前検査.失敗理由+" ログ: "+事前検査.ログパス);
            }
            var ケース一覧=this.選択ケース一覧
                .Where(ケース=>string.Equals(ケース.アセンブリ名,ジョブ.アセンブリ名,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(ケース.型完全名,ジョブ.型完全名,StringComparison.Ordinal))
                .ToArray();
            if(ケース一覧.Length==0) {
                throw new InvalidDataException($"AOTグループに選択ケースがありません: {ジョブ.アセンブリ名}.{ジョブ.型完全名}");
            }
            var ワーカーパス=AOTワーカーパスを解決();
            var アセンブリパス=Path.Combine(Path.GetDirectoryName(ワーカーパス)!,ジョブ.アセンブリ名+".dll");
            if(!File.Exists(アセンブリパス)) {
                throw new FileNotFoundException("AOTWorker publishにベンチマークアセンブリがありません。",アセンブリパス);
            }
            var グループ名=ベンチマークワーカークライアント.パス部分を無害化(ジョブ.アセンブリ名+"."+ジョブ.型完全名+".N"+ジョブ.要素数+".CPU"+論理CPU);
            var 成果物ディレクトリ=Path.Combine(実行ルート,"AOT",グループ名);
            Directory.CreateDirectory(成果物ディレクトリ);
            var ケース要求一覧=ケース一覧.Select(ケース=>ケース要求を作成(ケース,アセンブリパス,ジョブ.要素数)).ToArray();
            var 指紋元=JsonSerializer.Serialize(new {
                契約=AOT契約.現在バージョン,
                実行ルート=Path.GetFullPath(実行ルート),
                ジョブ.アセンブリ名,
                ジョブ.型完全名,
                ジョブ.要素数,
                論理CPU,
                ケース要求一覧,
                設定=new AOTジョブ設定(),
                事前検査.ツールチェーン指紋,
                Worker=ファイル指紋を作成(ワーカーパス),
                Benchmark=ファイル指紋を作成(アセンブリパス),
            },AOT契約.JSON設定);
            var 要求指紋=AOT契約.指紋を作成(指紋元);
            var 完了パス=Path.Combine(成果物ディレクトリ,AOT契約.完了ファイル名);
            if(完了応答を読み込む(完了パス,要求指紋,事前検査.ツールチェーン指紋,論理CPU,ケース要求一覧) is { } 完了応答) {
                return 完了応答.結果一覧.ToDictionary(static 結果=>(結果.メソッド名,結果.引数キー));
            }
            var 要求=new AOTワーカー要求(
                AOT契約.現在バージョン,
                AOT契約.指紋を作成(Path.GetFullPath(実行ルート)),
                ジョブ.アセンブリ名,
                アセンブリパス,
                ジョブ.型完全名,
                ケース要求一覧,
                論理CPU,
                成果物ディレクトリ,
                new(),
                事前検査.ツールチェーン指紋,
                要求指紋);
            AOT要求検証.検証(要求);
            var 要求パス=Path.Combine(成果物ディレクトリ,"request.json");
            JSONを原子的に保存(要求パス,要求);
            var タイムアウト=AOT契約.タイムアウトを計算(ケース要求一覧.Length);
            var 実行結果=await ワーカーを実行Async(ワーカーパス,["--request",要求パス],成果物ディレクトリ,タイムアウト,キャンセルトークン);
            var 応答パス=Path.Combine(成果物ディレクトリ,AOT契約.応答ファイル名);
            var 応答=JSONを読み込む<AOTワーカー応答>(応答パス)
                ??throw new FileNotFoundException("AOTワーカー応答がありません。",応答パス);
            if(実行結果!=0||!AOT応答検証.有効か(応答,要求指紋,事前検査.ツールチェーン指紋,論理CPU,ケース要求一覧)) {
                throw new InvalidOperationException($"AOTワーカーが失敗しました(終了コード={実行結果}): {応答.失敗理由} ログ: {応答.ログパス}");
            }
            return 応答.結果一覧.ToDictionary(static 結果=>(結果.メソッド名,結果.引数キー));
        } finally {
            this.実行ゲート.Release();
        }
    }

    private Task<AOT事前検査応答> 事前検査を取得Async(string 実行ルート,CancellationToken キャンセルトークン) {
        this.事前検査タスク??=new Lazy<Task<AOT事前検査応答>>(
            ()=>事前検査Async(実行ルート,キャンセルトークン),
            LazyThreadSafetyMode.ExecutionAndPublication);
        return this.事前検査タスク.Value;
    }

    private static async Task<AOT事前検査応答> 事前検査Async(string 実行ルート,CancellationToken キャンセルトークン) {
        var 出力=Path.Combine(実行ルート,"AOT","preflight");
        Directory.CreateDirectory(出力);
        var ワーカー=AOTワーカーパスを解決();
        var 終了コード=await ワーカーを実行Async(ワーカー,["--preflight","--output",出力],出力,TimeSpan.FromMinutes(20),キャンセルトークン);
        var 応答パス=Path.Combine(出力,AOT契約.事前検査応答ファイル名);
        var 応答=JSONを読み込む<AOT事前検査応答>(応答パス)
            ??throw new FileNotFoundException("AOT事前検査応答がありません。",応答パス);
        if(終了コード!=0||!string.Equals(応答.状態,"Completed",StringComparison.Ordinal)) {
            throw new InvalidOperationException($"NativeAOT事前検査に失敗しました(終了コード={終了コード}): {応答.失敗理由} ログ: {応答.ログパス}");
        }
        return 応答;
    }

    private static AOTケース要求 ケース要求を作成(ベンチマークケース ケース,string アセンブリパス,int 要素数) {
        var アセンブリ=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(候補=>string.Equals(候補.GetName().Name,ケース.アセンブリ名,StringComparison.OrdinalIgnoreCase))
            ??Assembly.LoadFrom(アセンブリパス);
        var 型=アセンブリ.GetType(ケース.型完全名,true,false)!;
        var メソッド=型.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Single(候補=>string.Equals(候補.Name,ケース.メソッド名,StringComparison.Ordinal));
        var パラメータ一覧=メソッド.GetParameters();
        if(パラメータ一覧.Length!=ケース.引数一覧.Count) {
            throw new InvalidDataException($"AOTケース引数数が一致しません: {ケース.型完全名}.{ケース.メソッド名}");
        }
        var 引数一覧=ケース.引数一覧.Select((引数,インデックス)=>new AOT引数要求(引数.名前,パラメータ一覧[インデックス].ParameterType.AssemblyQualifiedName??パラメータ一覧[インデックス].ParameterType.FullName??パラメータ一覧[インデックス].ParameterType.Name,引数.値)).ToArray();
        return new(ケース.プロジェクト,ケース.メソッド名,ケース.引数キー,引数一覧,要素数,ケース.バインド行インデックス);
    }

    private static AOTワーカー応答? 完了応答を読み込む(string パス,string 要求指紋,string ツールチェーン指紋,int 論理CPU,IReadOnlyList<AOTケース要求> ケース一覧) {
        var 応答=JSONを読み込む<AOTワーカー応答>(パス);
        if(応答 is not null&&AOT応答検証.有効か(応答,要求指紋,ツールチェーン指紋,論理CPU,ケース一覧)) {
            return 応答;
        }
        return null;
    }

    private static async Task<int> ワーカーを実行Async(string ワーカーパス,IReadOnlyList<string> 引数,string 出力,TimeSpan タイムアウト,CancellationToken キャンセルトークン) {
        var 開始情報=new ProcessStartInfo(ワーカーパス) {
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,
            StandardErrorEncoding=Encoding.UTF8,
            CreateNoWindow=true,
            WorkingDirectory=Path.GetDirectoryName(ワーカーパス)!,
        };
        foreach(var 値 in 引数) {
            開始情報.ArgumentList.Add(値);
        }
        using var プロセス=Process.Start(開始情報)??throw new InvalidOperationException("AOTワーカーを開始できませんでした。");
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
        await File.WriteAllTextAsync(Path.Combine(出力,"worker.stdout.log"),標準出力,new UTF8Encoding(false),CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(出力,"worker.stderr.log"),標準エラー,new UTF8Encoding(false),CancellationToken.None);
        if(タイムアウトしたか) {
            throw new TimeoutException($"AOTワーカーが{タイムアウト}後にタイムアウトしました。成果物: {出力}");
        }
        if(中断したか) {
            キャンセルトークン.ThrowIfCancellationRequested();
            throw new OperationCanceledException();
        }
        return プロセス.ExitCode;
    }

    private static string AOTワーカーパスを解決() {
        var パス=Path.Combine(AppContext.BaseDirectory,"AOTWorker","Worker.Aot.exe");
        if(File.Exists(パス)) {
            return パス;
        }
        throw new FileNotFoundException("完全publishされたAOTワーカーがありません。IDEのBenchmark.UI AOTプロファイルから再ビルドしてください。",パス);
    }

    private static string ファイル指紋を作成(string パス) {
        using var ストリーム=File.OpenRead(パス);
        return Convert.ToHexString(SHA256.HashData(ストリーム));
    }

    private static void JSONを原子的に保存<T>(string パス,T 値) {
        var 一時パス=パス+".tmp-"+Guid.NewGuid().ToString("N");
        File.WriteAllText(一時パス,JsonSerializer.Serialize(値,AOT契約.JSON設定),new UTF8Encoding(false));
        File.Move(一時パス,パス,overwrite:true);
    }

    private static T? JSONを読み込む<T>(string パス) {
        if(!File.Exists(パス)) {
            return default;
        }
        try {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(パス,Encoding.UTF8),AOT契約.JSON設定);
        } catch(JsonException) {
            return default;
        }
    }

    private static ベンチマークジョブ実行結果 失敗結果を作成(ベンチマークジョブ ジョブ,int 論理CPU,string 実行ルート,string 理由) => new(
        ジョブ.プロジェクト,ジョブ.メソッド名,ジョブ.型完全名,ジョブ.要素数,"Failed",string.Empty,string.Empty,string.Empty,null,null,null,null,1,実行ルート,理由,
        引数キー:ジョブ.引数キー,
        引数値一覧:ジョブ.引数一覧,
        ループ回数:1,

        アフィニティ適用成功か:false,
        実行項目インデックス:ジョブ.実行項目インデックス);

    private static string ナノ秒を整形(double 値) => 値.ToString("N3",CultureInfo.InvariantCulture)+" ns";
}
