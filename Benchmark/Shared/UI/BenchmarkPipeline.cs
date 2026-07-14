using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;

internal static class ベンチマークパイプライン定数 {
    public const int UI応答目標ミリ秒 = 100;
    public const int UIハートビート間隔ミリ秒 = 50;
    public const int UI描画間隔ミリ秒 = 100;
    public const int 進捗更新間隔ミリ秒 = 500;
    public const int チャート更新間隔ミリ秒 = 1000;
    public const int 永続化バッチ間隔ミリ秒 = 1000;
    public const int 一回の最大終端結果数 = 6;
}

/// <summary>
/// UIスレッドで実行された各区間の所要時間を集計し、5秒毎に1行のTraceレポートに出す。
/// どの区間がUIスレッド時間を支配しているかを、コンソールスパム無しで実測特定するための計器。
/// </summary>
internal static class UIスレッド予算 {
    private sealed class 項目 {
        public long 回数;
        public double 合計ミリ秒;
        public double 最大ミリ秒;
    }

    private static readonly ConcurrentDictionary<string,項目> 項目一覧 = new(StringComparer.Ordinal);

    public static void 記録(string 名前,double 経過ミリ秒) {
        var 項目 = 項目一覧.GetOrAdd(名前,static _ => new 項目());
        lock(項目) {
            項目.回数++;
            項目.合計ミリ秒+=経過ミリ秒;
            if(経過ミリ秒>項目.最大ミリ秒) {
                項目.最大ミリ秒=経過ミリ秒;
            }
        }
    }

    public static void 計測(string 名前,Action 処理) {
        var 開始時刻 = Stopwatch.GetTimestamp();
        try {
            処理();
        } finally {
            記録(名前,Stopwatch.GetElapsedTime(開始時刻).TotalMilliseconds);
        }
    }

    public static string 報告を取り出す() {
        var 項目配列 = 項目一覧.ToArray();
        項目一覧.Clear();
        if(項目配列.Length==0) {
            return string.Empty;
        }

        return string.Join(" | ",項目配列
            .OrderByDescending(static 項目 => 項目.Value.合計ミリ秒)
            .Take(8)
            .Select(static 項目 => $"{項目.Key}={項目.Value.合計ミリ秒:0}ms ×{項目.Value.回数} 最大={項目.Value.最大ミリ秒:0}"));
    }
}

internal sealed record ベンチマーク実行イベント(
    ベンチマーク結果項目 結果,
    int 実行世代,
    long? UI実行ID,
    DateTimeOffset 受信日時);

internal sealed record ベンチマーク永続化項目(
    ベンチマーク結果項目 結果,
    int 実行世代,
    long? UI実行ID);

internal sealed record ベンチマークUIスナップショット(
    IReadOnlyList<ベンチマーク実行イベント> 実行中更新一覧,
    IReadOnlyList<ベンチマーク実行イベント> 終端結果一覧,
    bool 進捗を更新するか,
    bool チャートを更新するか,
    ベンチマークパイプライン指標 指標) {
    public bool 更新ありか => this.実行中更新一覧.Count>0||this.終端結果一覧.Count>0||this.進捗を更新するか||this.チャートを更新するか;
}

internal sealed record ベンチマークパイプライン指標(
    long 受信イベント数,
    long 消費イベント数,
    long 描画イベント数,
    long 統合実行中イベント数,
    int 実行中更新数,
    int 終端滞留数,
    int 保留永続化イベント数,
    double 最終描画ミリ秒,
    double 最終永続化ミリ秒,
    double 最終ディスパッチャー遅延ミリ秒,
    long ディスパッチャー停止数,
    int 有効実行中数,
    int 変更実行中数,
    int 適用終端数);

internal sealed class ベンチマーク実行イベントパイプライン {
    private readonly Channel<ベンチマーク実行イベント> チャネル = Channel.CreateUnbounded<ベンチマーク実行イベント>(new UnboundedChannelOptions {
        SingleReader=true,
        SingleWriter=false,
        AllowSynchronousContinuations=false,
    });
    private readonly object 状態ゲート = new();
    private readonly Dictionary<string,ベンチマーク実行イベント> キー別実行中 = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> 変更実行中キー一覧 = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly List<ベンチマーク実行イベント> 終端直前実行中更新一覧 = [];
    private readonly Queue<ベンチマーク実行イベント> 終端結果一覧 = new();
    private long 受信イベント数;
    private long 消費イベント数;
    private long 描画イベント数;
    private long 統合実行中イベント数;
    private bool チャート変更ありか;
    private DateTimeOffset 最終進捗スナップショット日時 = DateTimeOffset.MinValue;
    private DateTimeOffset 最終チャートスナップショット日時 = DateTimeOffset.MinValue;

    public ベンチマーク実行イベントパイプライン() {
        _=Task.Run(this.消費Async);
    }

    public void イベントを送信(ベンチマーク実行イベント 項目) {
        Interlocked.Increment(ref this.受信イベント数);
        this.チャネル.Writer.TryWrite(項目);
    }

    public void クリア() {
        lock(this.状態ゲート) {
            this.キー別実行中.Clear();
            this.変更実行中キー一覧.Clear();
            this.終端直前実行中更新一覧.Clear();
            this.終端結果一覧.Clear();
            this.チャート変更ありか=false;
        }
    }

    public bool UIスナップショットを作成できるか(ベンチマークパイプライン指標 指標,out ベンチマークUIスナップショット スナップショット) {
        var 現在日時 = DateTimeOffset.UtcNow;
        lock(this.状態ゲート) {
            var 実行中一覧 = new List<ベンチマーク実行イベント>(this.変更実行中キー一覧.Count+this.終端直前実行中更新一覧.Count);
            実行中一覧.AddRange(this.終端直前実行中更新一覧);
            this.終端直前実行中更新一覧.Clear();
            foreach(var キー in this.変更実行中キー一覧) {
                if(this.キー別実行中.TryGetValue(キー,out var 項目)) {
                    実行中一覧.Add(項目);
                }
            }
            this.変更実行中キー一覧.Clear();
            var 終端一覧 = new List<ベンチマーク実行イベント>(ベンチマークパイプライン定数.一回の最大終端結果数);
            while(終端一覧.Count<ベンチマークパイプライン定数.一回の最大終端結果数&&this.終端結果一覧.Count>0) {
                終端一覧.Add(this.終端結果一覧.Dequeue());
            }

            var 進捗を更新するか = false;
            if((実行中一覧.Count>0||終端一覧.Count>0)&&現在日時-this.最終進捗スナップショット日時>=TimeSpan.FromMilliseconds(ベンチマークパイプライン定数.進捗更新間隔ミリ秒)) {
                進捗を更新するか=true;
                this.最終進捗スナップショット日時=現在日時;
            }

            var チャートを更新するか = false;
            if(this.チャート変更ありか&&現在日時-this.最終チャートスナップショット日時>=TimeSpan.FromMilliseconds(ベンチマークパイプライン定数.チャート更新間隔ミリ秒)) {
                チャートを更新するか=true;
                this.チャート変更ありか=false;
                this.最終チャートスナップショット日時=現在日時;
            }

            var 現在指標 = 指標 with {
                受信イベント数=Interlocked.Read(ref this.受信イベント数),
                消費イベント数=Interlocked.Read(ref this.消費イベント数),
                描画イベント数=Interlocked.Read(ref this.描画イベント数),
                統合実行中イベント数=Interlocked.Read(ref this.統合実行中イベント数),
                実行中更新数=実行中一覧.Count,
                終端滞留数=this.終端結果一覧.Count,
                有効実行中数=this.キー別実行中.Count,
                変更実行中数=実行中一覧.Count,
                適用終端数=終端一覧.Count,
            };
            スナップショット=new ベンチマークUIスナップショット(実行中一覧,終端一覧,進捗を更新するか,チャートを更新するか,現在指標);
            if(!スナップショット.更新ありか) {
                return false;
            }

            Interlocked.Add(ref this.描画イベント数,実行中一覧.Count+終端一覧.Count);
            return true;
        }
    }

    public bool 保留ありか {
        get {
            if(Interlocked.Read(ref this.受信イベント数)!=Interlocked.Read(ref this.消費イベント数)) {
                return true;
            }

            lock(this.状態ゲート) {
                return this.変更実行中キー一覧.Count>0||this.終端結果一覧.Count>0||this.チャート変更ありか;
            }
        }
    }

    public int 保留数 {
        get {
            lock(this.状態ゲート) {
                return this.変更実行中キー一覧.Count+this.終端結果一覧.Count;
            }
        }
    }

    private async Task 消費Async() {
        System.Diagnostics.Debug.WriteLine("[パイプライン] 消費Asyncを開始しました。");
        try {
            await foreach(var 項目 in this.チャネル.Reader.ReadAllAsync()) {
                Interlocked.Increment(ref this.消費イベント数);
                var 結果 = 項目.結果;
                lock(this.状態ゲート) {
                    if(実行中か(結果)) {
                        var キー = 実行中キーを作成(結果);
                        if(this.キー別実行中.ContainsKey(キー)) {
                            Interlocked.Increment(ref this.統合実行中イベント数);
                        }
                        this.キー別実行中[キー]=項目;
                        this.変更実行中キー一覧.Add(キー);
                    } else if(終端か(結果)) {
                        var キー = 実行中キーを作成(結果);
                        if(this.キー別実行中.TryGetValue(キー,out var 実行中項目)) {
                            this.終端直前実行中更新一覧.Add(実行中項目);
                        }
                        this.キー別実行中.Remove(キー);
                        this.変更実行中キー一覧.Remove(キー);
                        this.終端結果一覧.Enqueue(項目);
                        this.チャート変更ありか=true;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("[パイプライン] 消費Asyncのチャネルが正常に完了しました。");
        } catch(Exception 例外) {
            System.Diagnostics.Debug.WriteLine($"[パイプライン] 消費Asyncが失敗しました: {例外}");
            throw;
        }
    }

    private static bool 実行中か(ベンチマーク結果項目 結果) =>
        string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.実行中),StringComparison.OrdinalIgnoreCase);

    private static bool 終端か(ベンチマーク結果項目 結果) =>
        string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);

    private static string 実行中キーを作成(ベンチマーク結果項目 結果) =>
        string.Join("|",
            結果.ソースタブ,
            結果.プロジェクト,
            結果.型完全名,
            結果.メソッド,
            結果.引数キー,
            結果.軸数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            結果.割り当て論理CPU?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??"-");
}

internal sealed class ベンチマークUI診断(Action<string,Exception?> ログ) {
    private          long                      受信イベント数;
    private          long                      描画イベント数;
    private          long                      ディスパッチャー停止数;
    private          double                    最終ディスパッチャー遅延ミリ秒;
    private          double                    最終描画ミリ秒;
    private          double                    最終永続化ミリ秒;
    private          int                       実行中更新数;
    private          int                       終端滞留数;
    private          int                       保留永続化イベント数;
    private          int                       有効実行中数;
    private          int                       変更実行中数;
    private          int                       適用終端数;
    private          long                      統合実行中イベント数;
    private          long                      消費イベント数;
    private          CancellationTokenSource?  ハートビートキャンセル;
    private          Task?                     ハートビートタスク;
    public void ハートビートを開始(IベンチマークUIディスパッチャー ディスパッチャー) {
        if(this.ハートビートキャンセル is not null) {
            return;
        }

        var キャンセルソース = new CancellationTokenSource();
        this.ハートビートキャンセル=キャンセルソース;
        this.ハートビートタスク=Task.Run(async () => {
            var 期待日時 = DateTimeOffset.UtcNow+TimeSpan.FromMilliseconds(ベンチマークパイプライン定数.UIハートビート間隔ミリ秒);
            var 最終予算報告日時 = DateTimeOffset.UtcNow;
            while(!キャンセルソース.IsCancellationRequested) {
                try {
                    await Task.Delay(ベンチマークパイプライン定数.UIハートビート間隔ミリ秒,キャンセルソース.Token);
                } catch(OperationCanceledException) {
                    break;
                }

                var 報告日時 = DateTimeOffset.UtcNow;
                if(報告日時-最終予算報告日時>=TimeSpan.FromSeconds(5)) {
                    最終予算報告日時=報告日時;
                    var 報告 = UIスレッド予算.報告を取り出す();
                    if(!string.IsNullOrEmpty(報告)) {
                        System.Diagnostics.Debug.WriteLine("[UI][予算] "+報告);
                        // Trace出力だけではMAUIアプリ実行時にユーザーが確認できないため、startup.logにも残す。
                        ログ("[UI][予算] "+報告,null);
                    }
                }

                ディスパッチャー.呼び出し開始(() => {
                    var 現在日時 = DateTimeOffset.UtcNow;
                    var 遅延 = (現在日時-期待日時).TotalMilliseconds;
                    期待日時=現在日時+TimeSpan.FromMilliseconds(ベンチマークパイプライン定数.UIハートビート間隔ミリ秒);
                    if(遅延>ベンチマークパイプライン定数.UI応答目標ミリ秒) {
                        Interlocked.Increment(ref this.ディスパッチャー停止数);
                        this.最終ディスパッチャー遅延ミリ秒=遅延;
                        ログ("ベンチマークUIディスパッチャーが停止しました: "+遅延.ToString("0.0",global::System.Globalization.CultureInfo.InvariantCulture)+"ms",null);
                        System.Diagnostics.Debug.WriteLine($"[UI][ハートビート] 停止間隔={遅延:0.0}ms");
                    }
                });
            }
        });
    }

    public async Task ハートビートを停止Async() {
        var キャンセルソース = this.ハートビートキャンセル;
        if(キャンセルソース is null) {
            return;
        }

        this.ハートビートキャンセル=null;
        キャンセルソース.Cancel();
        var ハートビートタスク = this.ハートビートタスク;
        this.ハートビートタスク=null;
        try {
            if(ハートビートタスク is not null) {
                await ハートビートタスク;
            }
        } catch(OperationCanceledException) {
        } finally {
            キャンセルソース.Dispose();
        }
    }

    public void 受信イベントを記録() {
        Interlocked.Increment(ref this.受信イベント数);
    }

    public void 描画を記録(double 経過ミリ秒,ベンチマークパイプライン指標 指標,int 描画数) {
        this.最終描画ミリ秒=経過ミリ秒;
        this.実行中更新数=指標.実行中更新数;
        this.終端滞留数=指標.終端滞留数;
        this.有効実行中数=指標.有効実行中数;
        this.変更実行中数=指標.変更実行中数;
        this.適用終端数=指標.適用終端数;
        this.統合実行中イベント数=指標.統合実行中イベント数;
        this.消費イベント数=指標.消費イベント数;
        Interlocked.Add(ref this.描画イベント数,描画数);
    }

    public void 永続化を記録(double 経過ミリ秒,int 保留永続化イベント数) {
        this.最終永続化ミリ秒=経過ミリ秒;
        this.保留永続化イベント数=保留永続化イベント数;
    }

    public ベンチマークパイプライン指標 指標を作成(int 実行中更新数,int 終端滞留数,int 保留永続化イベント数) =>
        new(
            Interlocked.Read(ref this.受信イベント数),
            this.消費イベント数,
            Interlocked.Read(ref this.描画イベント数),
            this.統合実行中イベント数,
            実行中更新数,
            終端滞留数,
            保留永続化イベント数,
            this.最終描画ミリ秒,
            this.最終永続化ミリ秒,
            this.最終ディスパッチャー遅延ミリ秒,
            Interlocked.Read(ref this.ディスパッチャー停止数),
            this.有効実行中数,
            this.変更実行中数,
            this.適用終端数);

    public IReadOnlyList<ベンチマーク診断行> 行一覧を作成() {
        var 指標 = this.指標を作成(this.実行中更新数,this.終端滞留数,this.保留永続化イベント数);
        return [
            new ベンチマーク診断行("UI","ディスパッチャー遅延","目標",ベンチマークパイプライン定数.UI応答目標ミリ秒.ToString(global::System.Globalization.CultureInfo.InvariantCulture)+"ms",指標.最終ディスパッチャー遅延ミリ秒.ToString("0.0",global::System.Globalization.CultureInfo.InvariantCulture)+"ms",指標.ディスパッチャー停止数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),"ハートビート目標を超えた最後のディスパッチャー遅延。"),
            new ベンチマーク診断行("UI","描画バッチ","実行中",指標.実行中更新数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.最終描画ミリ秒.ToString("0.0",global::System.Globalization.CultureInfo.InvariantCulture)+"ms",指標.描画イベント数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),"非ブロッキングの100msスナップショット描画。"),
            new ベンチマーク診断行("UI","変更実行中","有効",指標.変更実行中数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.有効実行中数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.適用終端数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),"最後のスナップショットで適用した変更中・実行中・終端項目。"),
            new ベンチマーク診断行("イベント","統合実行中","Count",指標.統合実行中イベント数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.受信イベント数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.消費イベント数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),"キーごとに古い実行中更新を破棄した数。"),
            new ベンチマーク診断行("イベント","終端滞留","Count",指標.終端滞留数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.保留永続化イベント数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),指標.最終永続化ミリ秒.ToString("0.0",global::System.Globalization.CultureInfo.InvariantCulture)+"ms","完了／失敗の滞留とSQLiteバッチ。")
        ];
    }
}

internal sealed class ベンチマークUI描画スケジューラー(
    IベンチマークUIディスパッチャー        ディスパッチャー,
    ベンチマーク実行イベントパイプライン       パイプライン,
    ベンチマークUI診断               診断,
    Action<ベンチマークUIスナップショット> 適用) {
    private          int                      予約済みか;
    public void 描画を要求() {
        if(Interlocked.Exchange(ref this.予約済みか,1)==1) {
            return;
        }

        _=Task.Run(async () => {
            await Task.Delay(ベンチマークパイプライン定数.UI描画間隔ミリ秒);
            ディスパッチャー.呼び出し開始(this.一回描画);
        });
    }

    public void 即時反映() {
        ディスパッチャー.呼び出し開始(this.一回描画);
    }

    private void 一回描画() {
        Interlocked.Exchange(ref this.予約済みか,0);
        var 指標 = 診断.指標を作成(0,パイプライン.保留数,0);
        if(パイプライン.UIスナップショットを作成できるか(指標,out var スナップショット)) {
            var ストップウォッチ = Stopwatch.StartNew();
            適用(スナップショット);
            ストップウォッチ.Stop();
            診断.描画を記録(
                ストップウォッチ.Elapsed.TotalMilliseconds,
                スナップショット.指標,
                スナップショット.実行中更新一覧.Count+スナップショット.終端結果一覧.Count);
            UIスレッド予算.記録("描画合計",ストップウォッチ.Elapsed.TotalMilliseconds);
        }

        if(パイプライン.保留ありか) {
            this.描画を要求();
        }
    }
}

internal sealed class ベンチマーク永続化バッチ処理(
    IベンチマークUIディスパッチャー                  ディスパッチャー,
    ベンチマークUI診断                         診断,
    Action<IReadOnlyList<ベンチマーク永続化項目>> 永続化) {
    private readonly ConcurrentQueue<ベンチマーク永続化項目>       キュー     = new();
    private          int                                予約済みか;
    public void 追加(ベンチマーク永続化項目 項目) {
        this.キュー.Enqueue(項目);
        this.反映を要求(ベンチマークパイプライン定数.永続化バッチ間隔ミリ秒);
    }

    public void 即時反映() {
        Interlocked.Exchange(ref this.予約済みか,0);
        this.UIスレッドで反映();
    }

    private void 反映を要求(int 遅延ミリ秒) {
        if(Interlocked.Exchange(ref this.予約済みか,1)==1) {
            return;
        }

        _=Task.Run(async () => {
            if(遅延ミリ秒>0) {
                await Task.Delay(遅延ミリ秒);
            }

            ディスパッチャー.呼び出し開始(this.UIスレッドで反映);
        });
    }

    private void UIスレッドで反映() {
        Interlocked.Exchange(ref this.予約済みか,0);
        if(this.キュー.IsEmpty) {
            診断.永続化を記録(0,0);
            return;
        }

        var 項目一覧 = new List<ベンチマーク永続化項目>();
        while(this.キュー.TryDequeue(out var 項目)) {
            項目一覧.Add(項目);
        }

        var ストップウォッチ = Stopwatch.StartNew();
        永続化(項目一覧);
        ストップウォッチ.Stop();
        診断.永続化を記録(ストップウォッチ.Elapsed.TotalMilliseconds,this.キュー.Count);
        UIスレッド予算.記録("永続化フラッシュ",ストップウォッチ.Elapsed.TotalMilliseconds);
    }
}
