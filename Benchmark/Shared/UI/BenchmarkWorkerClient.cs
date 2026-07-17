using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace UI;

public sealed class ベンチマークワーカークライアント:Iベンチマークジョブ実行器 {
    public Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) =>
        this.実行Async(ジョブ.ベンチマークケースに変換(),実行ルート,ジョブ.ソースタブ,ジョブ.準備済み要素数一覧,ジョブ.要素数,ジョブ.ループ回数,"1",論理CPU,タイムアウト,キャンセルトークン,ジョブ.開放ループ較正か,ジョブ.較正閾値ナノ秒);

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークケース ベンチマーク,
        string 実行ルート,
        ベンチマークソースタブ ソースタブ,
        IReadOnlyList<int> 準備済み要素数一覧,
        int 要素数,
        int ループ回数,
        string 要素倍率,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン,
        bool 開放ループ較正か = false,
        double? 較正閾値ナノ秒 = null) {
        using var タイムアウトソース = new CancellationTokenSource(タイムアウト);
        using var 連結ソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
        var 有効トークン = 連結ソース.Token;
        try {
            await using var セッション = await this.セッション開始Async(実行ルート,論理CPU,有効トークン);
            var 結果 = await セッション.実行Async(ベンチマーク,ソースタブ,準備済み要素数一覧,要素数,ループ回数,要素倍率,有効トークン,開放ループ較正か,較正閾値ナノ秒);
            await セッション.終了Async(有効トークン);
            return 結果;
        } catch(OperationCanceledException) {
            if(タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested) {
                throw new TimeoutException($"ベンチマークジョブが {タイムアウト} 後にタイムアウトしました。");
            }
            throw;
        }
    }

    public async Task<ベンチマークワーカーセッション> セッション開始Async(
        string 実行ルート,
        int 論理CPU,
        CancellationToken キャンセルトークン) {
        var ワーカーパス = ワーカーパスを解決();
        var プロセス = ワーカーを開始(ワーカーパス,実行ルート,論理CPU);
        System.Diagnostics.Debug.WriteLine($"[ワーカー] ワーカー開始 PID={プロセス.Id} CPU={論理CPU} パス={ワーカーパス}");
        _=標準エラーを破棄Async(プロセス,論理CPU);
        try {
            var 準備応答 = await プロセス.StandardOutput.ReadLineAsync(キャンセルトークン);
            System.Diagnostics.Debug.WriteLine($"[ワーカー] セッション開始Async 準備応答受信: \"{準備応答}\" PID={プロセス.Id}");
            if(!string.Equals(準備応答,"ready",StringComparison.Ordinal)) {
                throw new InvalidOperationException($"ベンチマークワーカーが準備完了になりませんでした。最初の出力: {準備応答}");
            }

            return new ベンチマークワーカーセッション(プロセス,実行ルート);
        } catch {
            プロセスを終了(プロセス);
            プロセス.Dispose();
            throw;
        }
    }

    internal static async Task 標準エラーを破棄Async(Process プロセス,int 論理CPU) {
        try {
            while(await プロセス.StandardError.ReadLineAsync() is { } 行) {
                System.Diagnostics.Debug.WriteLine($"[ワーカー][標準エラー] CPU={論理CPU} PID={プロセス.Id}: {行}");
            }
        } catch {
        }
    }

    internal static string 成果物フォルダーを作成(string 実行ルート,ベンチマークケース ベンチマーク,ベンチマークソースタブ ソースタブ,int 要素数,int ループ回数) {
        var 成果物フォルダー = Path.Combine(
            実行ルート,
            ベンチマークソースタブ一覧.パス部分に変換(ソースタブ),
            "N"+要素数.ToString(CultureInfo.InvariantCulture),
            "Loop"+Math.Max(1,ループ回数).ToString(CultureInfo.InvariantCulture),
            パス部分を無害化(ベンチマーク.プロジェクト),
            パス部分を無害化(ベンチマーク.型完全名),
            パス部分を無害化(ベンチマーク.メソッド名),
            string.IsNullOrWhiteSpace(ベンチマーク.引数キー) ? "default" : パス部分を無害化(ベンチマーク.引数キー));
        Directory.CreateDirectory(成果物フォルダー);
        return 成果物フォルダー;
    }

    internal static Process ワーカーを開始(string ワーカーパス,string 実行ルート,int 論理CPU) {
        var DLLか = string.Equals(Path.GetExtension(ワーカーパス),".dll",StringComparison.OrdinalIgnoreCase);
        var 開始情報 = new ProcessStartInfo {
            FileName=DLLか ? "dotnet" : ワーカーパス,
            UseShellExecute=false,
            RedirectStandardInput=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,
            StandardErrorEncoding=Encoding.UTF8,
            CreateNoWindow=true,
        };

        if(DLLか) {
            開始情報.ArgumentList.Add(ワーカーパス);
        }

        開始情報.ArgumentList.Add("--logical-cpu");
        開始情報.ArgumentList.Add(論理CPU.ToString(CultureInfo.InvariantCulture));
        開始情報.ArgumentList.Add("--run-root");
        開始情報.ArgumentList.Add(実行ルート);

        return Process.Start(開始情報)
            ??throw new InvalidOperationException("ワーカーを開始できませんでした。");
    }

    internal static string ワーカーパスを解決() {
        var 基底ディレクトリ = AppContext.BaseDirectory;
        var EXEパス = Path.Combine(基底ディレクトリ,"Worker.exe");
        if(File.Exists(EXEパス)) {
            return EXEパス;
        }

        var DLLパス = Path.Combine(基底ディレクトリ,"Worker.dll");
        if(File.Exists(DLLパス)) {
            return DLLパス;
        }

        throw new FileNotFoundException("アプリケーション出力ディレクトリにワーカー実行ファイルが見つかりません。",EXEパス);
    }

    internal static string パス部分を無害化(string 文字列) {
        var 生成器 = new StringBuilder(文字列.Length);
        foreach(var 文字 in 文字列) {
            生成器.Append(Path.GetInvalidFileNameChars().Contains(文字) ? '_' : 文字);
        }

        return 生成器.ToString();
    }

    internal static void プロセスを終了(Process プロセス) {
        try {
            if(!プロセス.HasExited) {
                プロセス.Kill(entireProcessTree: true);
            }
        } catch(InvalidOperationException) {
        } catch(System.ComponentModel.Win32Exception) {
        }
    }
}

public sealed class ベンチマーク高速ワーカーセッション:IAsyncDisposable {
    private readonly Process プロセス;
    private readonly string 実行ルート;
    private readonly SemaphoreSlim 書込ゲート = new(1,1);
    private readonly ConcurrentDictionary<string,TaskCompletionSource<ワーカー結果メッセージ>> 保留一覧 = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource 終了ソース = new();
    private readonly Task 読取ループ;
    private bool 終了済みか;

    internal ベンチマーク高速ワーカーセッション(Process プロセス,string 実行ルート) {
        this.プロセス=プロセス;
        this.実行ルート=実行ルート;
        this.読取ループ=Task.Run(this.読取ループAsync);
    }

    public static async Task<ベンチマーク高速ワーカーセッション> 開始Async(string 実行ルート,CancellationToken キャンセルトークン) {
        var ワーカーパス = ベンチマークワーカークライアント.ワーカーパスを解決();
        var プロセス = ベンチマークワーカークライアント.ワーカーを開始(ワーカーパス,実行ルート,-1);
        System.Diagnostics.Debug.WriteLine($"[ワーカー] 高速ワーカー開始 PID={プロセス.Id} パス={ワーカーパス}");
        _=ベンチマークワーカークライアント.標準エラーを破棄Async(プロセス,-1);
        try {
            var 準備応答 = await プロセス.StandardOutput.ReadLineAsync(キャンセルトークン);
            System.Diagnostics.Debug.WriteLine($"[ワーカー] 高速セッション開始Async 準備応答受信: \"{準備応答}\" PID={プロセス.Id}");
            if(!string.Equals(準備応答,"ready",StringComparison.Ordinal)) {
                throw new InvalidOperationException($"ベンチマーク高速ワーカーが準備完了になりませんでした。最初の出力: {準備応答}");
            }

            return new ベンチマーク高速ワーカーセッション(プロセス,実行ルート);
        } catch {
            ベンチマークワーカークライアント.プロセスを終了(プロセス);
            プロセス.Dispose();
            throw;
        }
    }

    public async Task<ベンチマークジョブ実行結果> 実行Async(ベンチマークジョブ ジョブ,int 論理CPU,CancellationToken キャンセルトークン) {
        if(this.終了済みか) {
            throw new ObjectDisposedException(nameof(ベンチマーク高速ワーカーセッション));
        }

        var ベンチマーク = ジョブ.ベンチマークケースに変換();
        var ループ回数 = Math.Max(1,ジョブ.ループ回数);
        var 成果物フォルダー = ベンチマークワーカークライアント.成果物フォルダーを作成(this.実行ルート,ベンチマーク,ジョブ.ソースタブ,ジョブ.要素数,ループ回数);
        var タスクID = Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture);
        var 完了ソース = new TaskCompletionSource<ワーカー結果メッセージ>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!this.保留一覧.TryAdd(タスクID,完了ソース)) {
            throw new InvalidOperationException("高速ワーカーのタスクIDが重複しました。");
        }

        var メッセージ = new ワーカータスクメッセージ(
            "fast-run",
            タスクID,
            ベンチマーク.アセンブリ名,
            ベンチマーク.型完全名,
            ベンチマーク.メソッド名,
            ベンチマーク.引数キー,
            ベンチマーク.引数一覧.ToArray(),
            ジョブ.ソースタブ,
            ジョブ.準備済み要素数一覧.ToArray(),
            ジョブ.要素数,
            ループ回数,
            "1",
            成果物フォルダー,
            論理CPU,
            ジョブ.開放ループ較正か,
            ジョブ.較正閾値ナノ秒);
        using var 登録 = キャンセルトークン.Register(() => {
            this.終了();
            完了ソース.TrySetCanceled(キャンセルトークン);
        });
        try {
            await this.書込ゲート.WaitAsync(キャンセルトークン);
            try {
                await this.プロセス.StandardInput.WriteLineAsync(JsonSerializer.Serialize(メッセージ).AsMemory(),キャンセルトークン);
                await this.プロセス.StandardInput.FlushAsync(キャンセルトークン);
            } finally {
                this.書込ゲート.Release();
            }

            var 結果 = await 完了ソース.Task.WaitAsync(キャンセルトークン);
            return new ベンチマークジョブ実行結果(
                ベンチマーク.プロジェクト,
                ベンチマーク.メソッド名,
                ベンチマーク.型完全名,
                ジョブ.要素数,
                結果.状態,
                結果.平均,
                結果.誤差,
                結果.標準偏差,
                結果.平均ナノ秒,
                結果.誤差ナノ秒,
                結果.標準偏差ナノ秒,
                結果.割り当てバイト数,
                結果.終了コード,
                結果.成果物フォルダー,
                結果.ログ,
                結果.ランダムシード,
                結果.結果ダイジェスト,
                ベンチマーク.引数キー,
                ベンチマーク.引数一覧,
                結果.実測ループ回数??ループ回数,
                ジョブ.実行項目インデックス,
                結果.診断カウンター);
        } catch {
            this.保留一覧.TryRemove(タスクID,out _);
            throw;
        }
    }

    public async Task 終了Async(CancellationToken キャンセルトークン) {
        if(this.終了済みか) {
            return;
        }

        this.終了済みか=true;
        try {
            if(!this.プロセス.HasExited) {
                await this.書込ゲート.WaitAsync(キャンセルトークン);
                try {
                    await this.プロセス.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new ワーカータスクメッセージ("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,[],ベンチマークソースタブ.System配列,[],0,0,string.Empty,string.Empty)).AsMemory(),キャンセルトークン);
                    await this.プロセス.StandardInput.FlushAsync(キャンセルトークン);
                } finally {
                    this.書込ゲート.Release();
                }
                await this.プロセス.WaitForExitAsync(キャンセルトークン);
            }
        } catch {
            this.終了();
            throw;
        } finally {
            this.終了ソース.Cancel();
            await this.読取ループ完了Async();
            this.プロセス.Dispose();
        }
    }

    public async ValueTask DisposeAsync() {
        if(this.終了済みか) {
            this.プロセス.Dispose();
            return;
        }

        using var 終了ソース = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            await this.終了Async(終了ソース.Token);
        } catch {
            this.終了();
            this.プロセス.Dispose();
        } finally {
            this.終了ソース.Dispose();
            this.書込ゲート.Dispose();
        }
    }

    private async Task 読取ループAsync() {
        try {
            while(!this.終了ソース.IsCancellationRequested&&await this.プロセス.StandardOutput.ReadLineAsync(this.終了ソース.Token) is { } 行) {
                if(string.IsNullOrWhiteSpace(行)) {
                    continue;
                }

                var 結果 = JsonSerializer.Deserialize<ワーカー結果メッセージ>(行);
                if(結果 is null||string.IsNullOrWhiteSpace(結果.タスクID)) {
                    continue;
                }

                if(this.保留一覧.TryRemove(結果.タスクID,out var 完了ソース)) {
                    完了ソース.TrySetResult(結果);
                }
            }
        } catch(OperationCanceledException) {
        } catch(Exception 例外) {
            foreach(var 組 in this.保留一覧.ToArray()) {
                if(this.保留一覧.TryRemove(組.Key,out var 完了ソース)) {
                    完了ソース.TrySetException(例外);
                }
            }
        }
    }

    private async Task 読取ループ完了Async() {
        try {
            await this.読取ループ.WaitAsync(TimeSpan.FromSeconds(2));
        } catch {
        }
    }

    private void 終了() {
        this.終了済みか=true;
        this.終了ソース.Cancel();
        ベンチマークワーカークライアント.プロセスを終了(this.プロセス);
        foreach(var 組 in this.保留一覧.ToArray()) {
            if(this.保留一覧.TryRemove(組.Key,out var 完了ソース)) {
                完了ソース.TrySetCanceled();
            }
        }
    }
}

public sealed class ベンチマークワーカーセッション:IAsyncDisposable {
    private readonly Process プロセス;
    private readonly string 実行ルート;
    private bool 終了済みか;

    internal ベンチマークワーカーセッション(Process プロセス,string 実行ルート) {
        this.プロセス=プロセス;
        this.実行ルート=実行ルート;
    }

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークケース ベンチマーク,
        ベンチマークソースタブ ソースタブ,
        IReadOnlyList<int> 準備済み要素数一覧,
        int 要素数,
        int ループ回数,
        string 要素倍率,
        CancellationToken キャンセルトークン,
        bool 開放ループ較正か = false,
        double? 較正閾値ナノ秒 = null) {
        if(this.終了済みか) {
            throw new ObjectDisposedException(nameof(ベンチマークワーカーセッション));
        }

        var 成果物フォルダー = ベンチマークワーカークライアント.成果物フォルダーを作成(this.実行ルート,ベンチマーク,ソースタブ,要素数,ループ回数);
        var メッセージ = new ワーカータスクメッセージ(
            "run",
            Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            ベンチマーク.アセンブリ名,
            ベンチマーク.型完全名,
            ベンチマーク.メソッド名,
            ベンチマーク.引数キー,
            ベンチマーク.引数一覧.ToArray(),
            ソースタブ,
            準備済み要素数一覧.ToArray(),
            要素数,
            ループ回数,
            要素倍率,
            成果物フォルダー,
            開放ループ較正か: 開放ループ較正か,
            較正閾値ナノ秒: 較正閾値ナノ秒);

        try {
            System.Diagnostics.Debug.WriteLine($"[ワーカー] 実行Async送信 PID={this.プロセス.Id} メソッド={ベンチマーク.メソッド名} 要素数={要素数}");
            await this.プロセス.StandardInput.WriteLineAsync(JsonSerializer.Serialize(メッセージ).AsMemory(),キャンセルトークン);
            await this.プロセス.StandardInput.FlushAsync(キャンセルトークン);

            var 行 = await this.プロセス.StandardOutput.ReadLineAsync(キャンセルトークン)
                ??throw new InvalidOperationException("ベンチマークワーカーが結果を返さず終了しました。");
            System.Diagnostics.Debug.WriteLine($"[ワーカー] 実行Async受信 PID={this.プロセス.Id} メソッド={ベンチマーク.メソッド名} 行長={行.Length}");
            var 結果 = JsonSerializer.Deserialize<ワーカー結果メッセージ>(行)
                ??throw new InvalidOperationException("ベンチマークワーカーから空の結果が返されました。");
            return new ベンチマークジョブ実行結果(
                ベンチマーク.プロジェクト,
                ベンチマーク.メソッド名,
                ベンチマーク.型完全名,
                要素数,
                結果.状態,
                結果.平均,
                結果.誤差,
                結果.標準偏差,
                結果.平均ナノ秒,
                結果.誤差ナノ秒,
                結果.標準偏差ナノ秒,
                結果.割り当てバイト数,
                結果.終了コード,
                結果.成果物フォルダー,
                結果.ログ,
                結果.ランダムシード,
                結果.結果ダイジェスト,
                ベンチマーク.引数キー,
                ベンチマーク.引数一覧,
                結果.実測ループ回数??ループ回数,
                診断カウンター値一覧: 結果.診断カウンター);
        } catch(OperationCanceledException) {
            this.終了();
            throw;
        }
    }

    public async Task 終了Async(CancellationToken キャンセルトークン) {
        if(this.終了済みか) {
            return;
        }

        this.終了済みか=true;
        try {
            if(!this.プロセス.HasExited) {
                await this.プロセス.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new ワーカータスクメッセージ("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,[],ベンチマークソースタブ.System配列,[],0,0,string.Empty,string.Empty)).AsMemory(),キャンセルトークン);
                await this.プロセス.StandardInput.FlushAsync(キャンセルトークン);
                await this.プロセス.WaitForExitAsync(キャンセルトークン);
            }
        } catch {
            this.終了();
            throw;
        } finally {
            this.プロセス.Dispose();
        }
    }

    public async ValueTask DisposeAsync() {
        if(this.終了済みか) {
            this.プロセス.Dispose();
            return;
        }

        using var 終了ソース = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            await this.終了Async(終了ソース.Token);
        } catch {
            this.終了();
            this.プロセス.Dispose();
        }
    }

    private void 終了() {
        this.終了済みか=true;
        ベンチマークワーカークライアント.プロセスを終了(this.プロセス);
    }
}

internal sealed record ワーカータスクメッセージ(
    string コマンド,
    string タスクID,
    string アセンブリ名,
    string 型完全名,
    string メソッド名,
    string 引数キー,
    ベンチマーク引数[] 引数,
    ベンチマークソースタブ ソースタブ,
    int[] 準備済み要素数,
    int 要素数,
    int ループ回数,
    string 要素倍率,
    string 成果物フォルダー,
    int? 割り当て論理CPU = null,
    bool 開放ループ較正か = false,
    double? 較正閾値ナノ秒 = null);

internal sealed record ワーカー結果メッセージ(
    string タスクID,
    string 状態,
    string 平均,
    string 誤差,
    string 標準偏差,
    double? 平均ナノ秒,
    double? 誤差ナノ秒,
    double? 標準偏差ナノ秒,
    long? 割り当てバイト数,
    int 終了コード,
    string 成果物フォルダー,
    string ログ,
    int? ランダムシード = null,
    string 結果ダイジェスト = "",
    IReadOnlyList<ベンチマーク診断カウンター>? 診断カウンター = null,
    int? 実測ループ回数 = null);
