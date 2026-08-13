using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;

public interface Iベンチマークカタログサービス {
    Task<IReadOnlyList<ベンチマークケース>> 読込Async(CancellationToken キャンセルトークン);
}

public sealed class ベンチマークカタログサービス:Iベンチマークカタログサービス {
    public Task<IReadOnlyList<ベンチマークケース>> 読込Async(CancellationToken キャンセルトークン) {
        return Task.Run(() => {
            キャンセルトークン.ThrowIfCancellationRequested();
#if BENCHMARK_UI_TYPED_CATALOG
            return ベンチマークカタログ.読み込む(型付きベンチマークアセンブリカタログ.アセンブリ一覧);
#else
            return ベンチマークカタログ.読み込む();
#endif
        },キャンセルトークン);
    }
}

public interface ICPUトポロジーサービス {
    IReadOnlyList<CPU選択項目> 読み込む();
}

public sealed class CPUトポロジーサービス:ICPUトポロジーサービス {
    public IReadOnlyList<CPU選択項目> 読み込む() {
        return ベンチマークCPUトポロジープロバイダー.読み込む()
            .OrderBy(static CPU => CPU.論理プロセッサインデックス)
            .Select((CPU,番号) => new CPU選択項目(
                CPU.論理プロセッサインデックス,
                CPU.コア種別,
                CPU.物理コアインデックス,
                CPU.種別内コアインデックス,
                番号==0))
            .ToArray();
    }
}

public interface Iベンチマークワーカープロセスサービス:Iベンチマークジョブ実行器 {
}

public sealed class ベンチマークワーカープロセスサービス:Iベンチマークワーカープロセスサービス {
    private readonly ベンチマークワーカークライアント クライアント;

    public ベンチマークワーカープロセスサービス(double ウォームアップ目標ナノ秒 = 1_024_000_000d) {
        this.クライアント=new(7,ウォームアップ目標ナノ秒);
    }

    public Task<ベンチマークジョブ実行結果> 実行Async(ベンチマークジョブ ジョブ,string 実行ルート,int 論理CPU,TimeSpan タイムアウト,CancellationToken キャンセルトークン) =>
        this.クライアント.実行Async(ジョブ,実行ルート,論理CPU,タイムアウト,キャンセルトークン);

}

public sealed class ベンチマーク永続ワーカープロセスサービス:Iベンチマークジョブ実行器, Iベンチマーク動的CPUセット実行器, IAsyncDisposable {
    private readonly ベンチマークワーカークライアント クライアント;
    private readonly ConcurrentDictionary<int,ベンチマークワーカーセッション> セッション一覧 = new();
    private readonly ConcurrentDictionary<int,int> 実行中ジョブ数一覧 = new();

    public ベンチマーク永続ワーカープロセスサービス(double ウォームアップ目標ナノ秒 = 300_000_000d) {
        this.クライアント=new(1,ウォームアップ目標ナノ秒);
    }

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) {
        var セッション = await this.セッションを取得Async(実行ルート,論理CPU,キャンセルトークン);
        using var タイムアウトソース = new CancellationTokenSource(タイムアウト);
        using var 連結ソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
        this.実行中ジョブ数一覧.AddOrUpdate(論理CPU,1,static (_,件数) => 件数+1);
        try {
            var 結果 = await セッション.実行Async(ジョブ.ベンチマークケースに変換(),ジョブ.ソースタブ,ジョブ.準備済み要素数一覧,ジョブ.要素数,ジョブ.ループ回数,"1",連結ソース.Token,ジョブ.開放ループ較正か,ジョブ.較正閾値ナノ秒);
            return 結果 with { 実行項目インデックス=ジョブ.実行項目インデックス };
        } catch(OperationCanceledException) {
            await this.セッションを再設定Async(論理CPU,セッション);
            if(タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested) {
                throw new TimeoutException($"ベンチマークジョブが {タイムアウト} 後にタイムアウトしました。");
            }

            throw;
        } catch {
            await this.セッションを再設定Async(論理CPU,セッション);
            throw;
        } finally {
            this.実行中ジョブ数一覧.AddOrUpdate(論理CPU,0,static (_,件数) => Math.Max(0,件数-1));
        }
    }

    public async Task CPUセットを適用Async(IReadOnlyList<int> 論理CPU一覧) {
        var 有効CPU一覧 = 論理CPU一覧.ToHashSet();
        foreach(var 組 in this.セッション一覧.ToArray()) {
            if(有効CPU一覧.Contains(組.Key)||this.実行中ジョブ数一覧.GetValueOrDefault(組.Key)>0) {
                continue;
            }

            await this.セッションを再設定Async(組.Key,組.Value);
        }
    }

    public async ValueTask DisposeAsync() {
        var セッション一覧 = this.セッション一覧.Values.ToArray();
        this.セッション一覧.Clear();
        foreach(var セッション in セッション一覧) {
            await セッション.DisposeAsync();
        }
    }

    private async Task<ベンチマークワーカーセッション> セッションを取得Async(
        string 実行ルート,
        int 論理CPU,
        CancellationToken キャンセルトークン) {
        if(this.セッション一覧.TryGetValue(論理CPU,out var 既存セッション)) {
            return 既存セッション;
        }

        var 作成セッション = await this.クライアント.セッション開始Async(実行ルート,論理CPU,キャンセルトークン);
        if(this.セッション一覧.TryAdd(論理CPU,作成セッション)) {
            return 作成セッション;
        }

        await 作成セッション.DisposeAsync();
        return this.セッション一覧[論理CPU];
    }

    private async Task セッションを再設定Async(int 論理CPU,ベンチマークワーカーセッション セッション) {
        if(this.セッション一覧.TryGetValue(論理CPU,out var 現在セッション)
            &&ReferenceEquals(現在セッション,セッション)
            &&this.セッション一覧.TryRemove(論理CPU,out _)) {
            await セッション.DisposeAsync();
        }
    }
}

public sealed class ベンチマーク高速ワーカープロセスサービス:Iベンチマークジョブ実行器, IAsyncDisposable {
    private readonly SemaphoreSlim セッションゲート = new(1,1);
    private ベンチマーク高速ワーカーセッション? セッション;
    private string 実行ルート = string.Empty;
    private readonly double ウォームアップ目標ナノ秒;

    public ベンチマーク高速ワーカープロセスサービス(double ウォームアップ目標ナノ秒 = 300_000_000d) {
        if(!double.IsFinite(ウォームアップ目標ナノ秒)||ウォームアップ目標ナノ秒<=0d) {
            throw new ArgumentOutOfRangeException(nameof(ウォームアップ目標ナノ秒));
        }

        this.ウォームアップ目標ナノ秒=ウォームアップ目標ナノ秒;
    }

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) {
        var 現在セッション = await this.セッションを取得Async(実行ルート,キャンセルトークン);
        using var タイムアウトソース = new CancellationTokenSource(タイムアウト);
        using var 連結ソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
        try {
            return await 現在セッション.実行Async(ジョブ,論理CPU,連結ソース.Token);
        } catch(OperationCanceledException) {
            await this.セッションを再設定Async(現在セッション);
            if(タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested) {
                throw new TimeoutException($"ベンチマークジョブが {タイムアウト} 後にタイムアウトしました。");
            }

            throw;
        } catch {
            await this.セッションを再設定Async(現在セッション);
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await this.セッションゲート.WaitAsync();
        try {
            if(this.セッション is not null) {
                await this.セッション.DisposeAsync();
                this.セッション=null;
            }
        } finally {
            this.セッションゲート.Release();
            this.セッションゲート.Dispose();
        }
    }

    private async Task<ベンチマーク高速ワーカーセッション> セッションを取得Async(string 実行ルート,CancellationToken キャンセルトークン) {
        await this.セッションゲート.WaitAsync(キャンセルトークン);
        try {
            if(this.セッション is not null&&string.Equals(this.実行ルート,実行ルート,StringComparison.OrdinalIgnoreCase)) {
                return this.セッション;
            }

            if(this.セッション is not null) {
                await this.セッション.DisposeAsync();
                this.セッション=null;
            }

            this.実行ルート=実行ルート;
            this.セッション=await ベンチマーク高速ワーカーセッション.開始Async(
                実行ルート,
                this.ウォームアップ目標ナノ秒,
                キャンセルトークン);
            return this.セッション;
        } finally {
            this.セッションゲート.Release();
        }
    }

    private async Task セッションを再設定Async(ベンチマーク高速ワーカーセッション 現在セッション) {
        await this.セッションゲート.WaitAsync();
        try {
            if(ReferenceEquals(this.セッション,現在セッション)) {
                this.セッション=null;
                await 現在セッション.DisposeAsync();
            }
        } finally {
            this.セッションゲート.Release();
        }
    }
}

internal sealed class 旧ベンチマークAOTワーカープロセスサービス:Iベンチマークジョブ実行器, IAsyncDisposable {
    // NativeAotToolchainによるILCビルドは1つの(アセンブリ,型)ペアにつき数十秒〜10分かかる。
    // 呼び出し元の`要求.ジョブタイムアウト`は全プロファイル共通で60分固定であり(BenchmarkRunnerServices.cs
    // の実行Async内`TimeSpan.FromMinutes(60)`)、型単位バッチビルドの待受にもそのまま流用できる長さがある。
    private readonly SemaphoreSlim ビルドゲート = new(1,1);
    private readonly ConcurrentDictionary<string,Lazy<Task<IReadOnlyDictionary<(string メソッド名,string 引数キー),Aot結果JSON解析器.Aotベンチマーク結果>>>> 型別結果キャッシュ = new(StringComparer.Ordinal);

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) {
        var 型別結果 = await this.型別結果を取得Async(ジョブ.アセンブリ名,ジョブ.型完全名,実行ルート,タイムアウト,キャンセルトークン);
        var キー = (ジョブ.メソッド名,ジョブ.引数キー);
        if(!型別結果.TryGetValue(キー,out var 結果)) {
            return new ベンチマークジョブ実行結果(
                ジョブ.プロジェクト,
                ジョブ.メソッド名,
                ジョブ.型完全名,
                ジョブ.要素数,
                "Failed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                string.Empty,
                $"AOT測定結果に{ジョブ.型完全名}.{ジョブ.メソッド名}(引数キー=\"{ジョブ.引数キー}\")が見つかりませんでした。",
                引数キー:ジョブ.引数キー,
                引数値一覧:ジョブ.引数一覧,
                ループ回数:1,
                実行項目インデックス:ジョブ.実行項目インデックス);
        }

        return new ベンチマークジョブ実行結果(
            ジョブ.プロジェクト,
            ジョブ.メソッド名,
            ジョブ.型完全名,
            ジョブ.要素数,
            "Completed",
            ナノ秒を整形(結果.平均ナノ秒),
            ナノ秒を整形(結果.誤差ナノ秒),
            ナノ秒を整形(結果.標準偏差ナノ秒),
            結果.平均ナノ秒,
            結果.誤差ナノ秒,
            結果.標準偏差ナノ秒,
            結果.割り当てバイト数,
            0,
            実行ルート,
            string.Empty,
            引数キー:ジョブ.引数キー,
            引数値一覧:ジョブ.引数一覧,
            ループ回数:1,
            実行項目インデックス:ジョブ.実行項目インデックス);
    }

    public async ValueTask DisposeAsync() {
        await this.ビルドゲート.WaitAsync();
        this.ビルドゲート.Release();
        this.ビルドゲート.Dispose();
    }

    private Task<IReadOnlyDictionary<(string,string),Aot結果JSON解析器.Aotベンチマーク結果>> 型別結果を取得Async(
        string アセンブリ名,string 型完全名,string 実行ルート,TimeSpan タイムアウト,CancellationToken キャンセルトークン) {
        var キャッシュキー = アセンブリ名+"|"+型完全名;
        var 遅延タスク = this.型別結果キャッシュ.GetOrAdd(
            キャッシュキー,
            _ => new Lazy<Task<IReadOnlyDictionary<(string,string),Aot結果JSON解析器.Aotベンチマーク結果>>>(
                () => this.型を測定Async(アセンブリ名,型完全名,実行ルート,タイムアウト,キャンセルトークン),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return this.失敗時にキャッシュから除去Async(キャッシュキー,遅延タスク.Value);
    }

    private async Task<IReadOnlyDictionary<(string,string),Aot結果JSON解析器.Aotベンチマーク結果>> 失敗時にキャッシュから除去Async(
        string キャッシュキー,Task<IReadOnlyDictionary<(string,string),Aot結果JSON解析器.Aotベンチマーク結果>> タスク) {
        try {
            return await タスク;
        } catch {
            this.型別結果キャッシュ.TryRemove(キャッシュキー,out _);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<(string,string),Aot結果JSON解析器.Aotベンチマーク結果>> 型を測定Async(
        string アセンブリ名,string 型完全名,string 実行ルート,TimeSpan タイムアウト,CancellationToken キャンセルトークン) {
        await this.ビルドゲート.WaitAsync(キャンセルトークン);
        try {
            var 出力ディレクトリ = Path.Combine(実行ルート,"AOT",ベンチマークワーカークライアント.パス部分を無害化(アセンブリ名+"."+型完全名));
            Directory.CreateDirectory(出力ディレクトリ);
            var ワーカーパス = AOTワーカーパスを解決();
            var 開始情報 = new ProcessStartInfo {
                FileName=ワーカーパス,
                UseShellExecute=false,
                RedirectStandardOutput=true,
                RedirectStandardError=true,
                StandardOutputEncoding=Encoding.UTF8,
                StandardErrorEncoding=Encoding.UTF8,
                CreateNoWindow=true,
            };
            開始情報.ArgumentList.Add(アセンブリ名);
            開始情報.ArgumentList.Add(型完全名);
            開始情報.ArgumentList.Add(出力ディレクトリ);

            using var プロセス = Process.Start(開始情報)??throw new InvalidOperationException("AOTワーカーを開始できませんでした。");
            _=標準ストリームを破棄Async(プロセス.StandardOutput);
            _=標準ストリームを破棄Async(プロセス.StandardError);

            using var タイムアウトソース = new CancellationTokenSource(タイムアウト);
            using var 連結ソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
            try {
                await プロセス.WaitForExitAsync(連結ソース.Token);
            } catch(OperationCanceledException) {
                try {
                    プロセス.Kill(true);
                } catch {
                }
                if(タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested) {
                    throw new TimeoutException($"AOTビルドが型 {アセンブリ名}.{型完全名} に対して {タイムアウト} 後にタイムアウトしました。");
                }
                throw;
            }

            if(プロセス.ExitCode!=0) {
                throw new InvalidOperationException($"AOTワーカーが型 {アセンブリ名}.{型完全名} の測定に失敗しました(終了コード={プロセス.ExitCode})。");
            }

            var 結果JSONパス = Path.Combine(出力ディレクトリ,"results",型完全名+"-report-full.json");
            if(!File.Exists(結果JSONパス)) {
                throw new FileNotFoundException($"AOT測定結果JSONが見つかりません: {結果JSONパス}");
            }

            var 解析結果一覧 = Aot結果JSON解析器.解析(結果JSONパス);
            return 解析結果一覧.ToDictionary(static 結果 => (結果.メソッド名,結果.引数キー));
        } finally {
            this.ビルドゲート.Release();
        }
    }

    private static async Task 標準ストリームを破棄Async(StreamReader ストリーム) {
        try {
            while(await ストリーム.ReadLineAsync() is { }) {
            }
        } catch {
        }
    }

    private static string AOTワーカーパスを解決() {
        var 基底ディレクトリ = AppContext.BaseDirectory;
        var EXEパス = Path.Combine(基底ディレクトリ,"Worker.Aot.exe");
        if(File.Exists(EXEパス)) {
            return EXEパス;
        }

        throw new FileNotFoundException("アプリケーション出力ディレクトリにAOTワーカー実行ファイルが見つかりません。",EXEパス);
    }

    private static string ナノ秒を整形(double? 値) {
        return 値 is { } 数値&&double.IsFinite(数値)
            ? 数値.ToString("N3",CultureInfo.InvariantCulture)+" ns"
            : string.Empty;
    }
}

public interface Iベンチマーク結果サービス {
    報告行 報告行に変換(ベンチマーク結果項目 項目);
}

public sealed class ベンチマーク結果サービス:Iベンチマーク結果サービス {
    public 報告行 報告行に変換(ベンチマーク結果項目 項目) {
        var メソッド = 項目.要素数>0
            ? 項目.メソッド+" [N="+項目.要素数.ToString(CultureInfo.InvariantCulture)+"]"
            : 項目.メソッド;
        return new 報告行(
            項目.プロジェクト,
            メソッド,
            項目.型完全名,
            メソッド,
            項目.状態,
            項目.平均,
            string.Empty,
            string.Empty,
            項目.割り当てバイト数値?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            項目.平均ナノ秒??0,
            項目.誤差ナノ秒??0,
            項目.標準偏差ナノ秒??0,
            項目.割り当てバイト数値??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            項目.成果物フォルダー,
            項目.終了コード,
            項目.引数キー,
            項目.引数一覧);
    }
}

public interface Iベンチマーク比較サービス {
    IReadOnlyList<ベンチマーク比較行> 比較行を構築(IEnumerable<ベンチマーク結果項目> 結果一覧);
}

public sealed class ベンチマーク比較サービス(Iベンチマーク結果サービス 結果サービス):Iベンチマーク比較サービス {
    public IReadOnlyList<ベンチマーク比較行> 比較行を構築(IEnumerable<ベンチマーク結果項目> 結果一覧) {
        var 報告行一覧 = 結果一覧.Select(結果サービス.報告行に変換).ToArray();
        return ベンチマーク比較フォーマッター.比較行を構築(報告行一覧);
    }

}

public interface Iベンチマーク実行サービス {
    Task<ベンチマーク実行報告> 実行Async(
        IReadOnlyList<ベンチマークケース> ベンチマーク一覧,
        IReadOnlyList<int> 論理CPU一覧,
        Func<IReadOnlyList<int>> 論理CPU取得器,
        IReadOnlyList<int> 要素数一覧,
        ベンチマークソースタブ ソースタブ,
        ベンチマーク実行プロファイル 実行プロファイル,
        TimeSpan 適応しきい値,
        bool 要求要素数から再開するか,
        string? 実行ルート上書き,
        Func<IReadOnlyList<ベンチマークケース>>? ベンチマーク取得器,
        Action<ベンチマーク結果項目> 結果受信時,
        Action<string> 状態変更時,
        ベンチマーク実行オプション? 実行オプション,
        CancellationToken キャンセルトークン);
}

public sealed class ベンチマーク実行サービス(Iベンチマークワーカープロセスサービス ワーカーサービス):Iベンチマーク実行サービス {
    public async Task<ベンチマーク実行報告> 実行Async(
        IReadOnlyList<ベンチマークケース> ベンチマーク一覧,
        IReadOnlyList<int> 論理CPU一覧,
        Func<IReadOnlyList<int>> 論理CPU取得器,
        IReadOnlyList<int> 要素数一覧,
        ベンチマークソースタブ ソースタブ,
        ベンチマーク実行プロファイル 実行プロファイル,
        TimeSpan 適応しきい値,
        bool 要求要素数から再開するか,
        string? 実行ルート上書き,
        Func<IReadOnlyList<ベンチマークケース>>? ベンチマーク取得器,
        Action<ベンチマーク結果項目> 結果受信時,
        Action<string> 状態変更時,
        ベンチマーク実行オプション? 実行オプション,
        CancellationToken キャンセルトークン) {
        var オーケストレーションルート = ソリューションルートを検索()??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var ベンチマークルート = Path.Combine(オーケストレーションルート,"Work",ベンチマークアプリプロファイル.作業ルート名,ベンチマーク実行プロファイル名.フォルダー名に変換(実行プロファイル));
        var ジョブ実行器 = this.ジョブ実行器を作成(実行プロファイル,適応しきい値,実行オプション,ベンチマーク一覧);
        var オーケストレーター = new ベンチマークオーケストレーター(
            new ベンチマーク状態マネージャー(ベンチマークルート),
            ジョブ実行器);
        var 要求 = new ベンチマーク実行要求(
            ベンチマーク一覧,
            論理CPU一覧,
            要素数一覧,
            適応しきい値,
            TimeSpan.FromMinutes(60),
            論理CPU取得器,
            要求要素数から再開するか: 要求要素数から再開するか,
            実行プロファイル: 実行プロファイル,
            実行ルート上書き: 実行ルート上書き,
            ソースタブ: ソースタブ,
            ベンチマーク取得器: ベンチマーク取得器);
        try {
            var オーケストレーター結果 = await オーケストレーター.実行Async(
                要求,
                更新 => 結果受信時(new ベンチマーク結果項目(更新.ジョブ)),
                状態変更時,
                キャンセルトークン);
            return new ベンチマーク実行報告(オーケストレーター結果.実行ルート);
        } finally {
            if(ジョブ実行器 is IAsyncDisposable 非同期破棄対象) {
                await 非同期破棄対象.DisposeAsync();
            } else if(ジョブ実行器 is IDisposable 破棄対象) {
                破棄対象.Dispose();
            }
        }

    }

    private Iベンチマークジョブ実行器 ジョブ実行器を作成(
        ベンチマーク実行プロファイル 実行プロファイル,
        TimeSpan 適応しきい値,
        ベンチマーク実行オプション? 実行オプション,
        IReadOnlyList<ベンチマークケース> ベンチマーク一覧) =>
        実行プロファイル switch {
            ベンチマーク実行プロファイル.正確 => ワーカーサービス,
            ベンチマーク実行プロファイル.中間 => new ベンチマーク永続ワーカープロセスサービス(適応しきい値.TotalMilliseconds*1_000_000d),
            ベンチマーク実行プロファイル.高速 => new ベンチマーク高速ワーカープロセスサービス(適応しきい値.TotalMilliseconds*1_000_000d),
            ベンチマーク実行プロファイル.テスト => new ベンチマークテスト待機ジョブ実行器(適応しきい値,実行オプション?.無限テスト実行器を使うか==true),
            ベンチマーク実行プロファイル.AOT => new ベンチマークAOTワーカープロセスサービス(ベンチマーク一覧),
            _ => ワーカーサービス,
        };

    private static string? ソリューションルートを検索() {
        var ディレクトリ = new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null) {
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
                return ディレクトリ.FullName;
            }
            ディレクトリ=ディレクトリ.Parent;
        }

        return null;
    }

}
