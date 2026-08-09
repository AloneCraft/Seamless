using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class ベンチマークオーケストレーター(
    ベンチマーク状態マネージャー 状態マネージャー,
    Iベンチマークジョブ実行器 ジョブ実行器) {
    private readonly Lock 状態排他 = new();
    public async Task<ベンチマークオーケストレーター結果> 実行Async(
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        var 状態 = !string.IsNullOrWhiteSpace(要求.実行ルート上書き)
            ? 状態マネージャー.読み込む(要求.実行ルート上書き)??this.状態を作成(要求)
            : this.状態を作成(要求);
        this.状態を要求と同期(状態,要求);
        状態マネージャー.保存(状態);

        try {
            await this.適応実行Async(状態,要求,進行通知,状態変更時,キャンセルトークン);
        } catch(OperationCanceledException) when(キャンセルトークン.IsCancellationRequested) {
            foreach(var ジョブ in 状態.ジョブ一覧.Where(static ジョブ=>ジョブ.状態==ジョブ状態.実行中)) {
                ジョブ.状態=ジョブ状態.保留;
                ジョブ.割り当て論理CPU=null;
                ジョブ.開始日時=null;
                ジョブ.失敗理由=string.Empty;
            }
            状態マネージャー.保存(状態);
            throw;
        }

        return new ベンチマークオーケストレーター結果(状態.実行ルート,状態);
    }

    private async Task 適応実行Async(
        進行状態 状態,
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        var キュー = new ConcurrentQueue<適応メソッド行>();
        var キュー済みキー一覧 = new global::System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var 完了キー一覧 = new global::System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await this.動的適応行を実行Async(
            状態,
            キュー,
            キュー済みキー一覧,
            完了キー一覧,
            要求,
            進行通知,
            状態変更時,
            キャンセルトークン);
    }

    private async Task 動的適応行を実行Async(
        進行状態 状態,
        ConcurrentQueue<適応メソッド行> キュー,
        global::System.Collections.Generic.HashSet<string> キュー済みキー一覧,
        global::System.Collections.Generic.HashSet<string> 完了キー一覧,
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        var 実行中数 = 0;
        var ワーカー一覧 = new Dictionary<int,適応ワーカー>();
        try {
            while(true) {
                キャンセルトークン.ThrowIfCancellationRequested();
                foreach(var ワーカー in ワーカー一覧.Where(static 項目=>項目.Value.完了か).ToArray()) {
                    if(ワーカー.Value.タスク.IsFaulted) {
                        await ワーカー.Value.タスク;
                    }

                    lock(キュー済みキー一覧) {
                        完了キー一覧.Add(ワーカー.Value.行キー);
                    }

                    ワーカー一覧.Remove(ワーカー.Key);
                }

                this.現在の適応行をキューへ追加(状態,キュー,キュー済みキー一覧,完了キー一覧,要求);
                var 現在CPU一覧 = 現在CPU一覧を取得(要求);
                if(ジョブ実行器 is Iベンチマーク動的CPUセット実行器 動的CPUセット実行器) {
                    await 動的CPUセット実行器.CPUセットを適用Async(現在CPU一覧);
                }

                foreach(var CPU in 現在CPU一覧) {
                    if(ワーカー一覧.ContainsKey(CPU)) {
                        continue;
                    }

                    if(!this.現在の適応行取得を試みる(状態,キュー,キュー済みキー一覧,要求,out var 行)) {
                        continue;
                    }

                    ワーカー一覧.Add(CPU,new 適応ワーカー(行.キー,Task.Run(async () => {
                        Interlocked.Increment(ref 実行中数);
                        System.Diagnostics.Debug.WriteLine($"[オーケストレーター] ワーカー開始 CPU={CPU} 行={行.キー}");
                        try {
                            await this.適応メソッド行を実行Async(状態,行,CPU,要求,進行通知,状態変更時,キャンセルトークン);
                            System.Diagnostics.Debug.WriteLine($"[オーケストレーター] ワーカー終了 CPU={CPU} 行={行.キー}");
                        } catch(Exception 例外) {
                            System.Diagnostics.Debug.WriteLine($"[オーケストレーター] ワーカー失敗 CPU={CPU} 行={行.キー}: {例外}");
                            throw;
                        } finally {
                            Interlocked.Decrement(ref 実行中数);
                        }
                    },キャンセルトークン)));
                }

                if(キュー.IsEmpty&&Volatile.Read(ref 実行中数)==0&&ワーカー一覧.Count==0) {
                    break;
                }

                await Task.Delay(25,キャンセルトークン);
            }

            await Task.WhenAll(ワーカー一覧.Values.Select(static ワーカー=>ワーカー.タスク));
        } finally {
            // キャンセルで while を抜けた場合でも、置き去りのワーカーが自身の状態復帰
            // （保留への巻き戻し・状態保存）を終えるまで待つ。待たずに戻ると、呼び出し元が
            // 一時停止完了直後に再開した際、まだ実行中のジョブを状態マネージャーの読み込み処理が
            // クラッシュ復旧対象と判断し、失敗へ誤確定させてしまう。
            try {
                await Task.WhenAll(ワーカー一覧.Values.Select(static ワーカー=>ワーカー.タスク)).ConfigureAwait(false);
            } catch {
                // 個々のワーカーの例外はここでは無視する（状態復帰は各ワーカー自身で完了済み）。
                // 元の例外(キャンセル等)を隠さないため握りつぶす。
            }
        }
    }

    private async Task 適応メソッド行を実行Async(
        進行状態 状態,
        適応メソッド行 行,
        int 論理CPU,
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        if(ベンチマーク作業軸.ループ回数を使うか(行.ソースタブ)) {
            await this.右較正ループ回数行を実行Async(状態,行,論理CPU,要求,進行通知,状態変更時,キャンセルトークン);
            return;
        }

        await this.スケーラブル行を実行Async(状態,行,論理CPU,要求,進行通知,状態変更時,キャンセルトークン);
    }

    private async Task 右較正ループ回数行を実行Async(
        進行状態 状態,
        適応メソッド行 行,
        int 論理CPU,
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        if(行.ケース一覧.Count==0) {
            return;
        }

        var ウォームアップ済みケース一覧 = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var 較正ケース = 行.ケース一覧[^1];
        var 同期ケース一覧 = 行.ケース一覧.Take(行.ケース一覧.Count-1).ToArray();

        if(要求.実行プロファイル is ベンチマーク実行プロファイル.AOT) {
            // NativeAotToolchainによるBenchmarkDotNet標準測定はケースごとに自己完結しており、
            // ZLinq側を較正してFLinq側と同じループ回数で本測定するという既存の「較正→同期」構造が
            // そもそも不要(較正ループ自体を使わない)。行内の全ケースを独立に1回ずつ実行するだけでよい。
            await this.AOTで行を実行Async(状態,行,論理CPU,要求,進行通知,状態変更時,キャンセルトークン);
            return;
        }

        if(要求.実行プロファイル is ベンチマーク実行プロファイル.高速 or ベンチマーク実行プロファイル.正確 or ベンチマーク実行プロファイル.中間) {
            await this.開放ループ較正で行を実行Async(状態,行,較正ケース,同期ケース一覧,論理CPU,要求,ウォームアップ済みケース一覧,進行通知,状態変更時,キャンセルトークン);
            return;
        }

        foreach(var ループ回数 in 適応ループ回数を列挙(状態)) {
            キャンセルトークン.ThrowIfCancellationRequested();
            var 較正ジョブ = this.既存適応ジョブ一覧を取得(状態,[較正ケース],ループ回数).FirstOrDefault();
            if(較正ジョブ is { 状態: ジョブ状態.失敗 }) {
                return;
            }

            if(!較正完了か(較正ジョブ,状態.適応しきい値ナノ秒)) {
                較正ジョブ=this.適応ジョブ一覧を取得または作成(状態,[較正ケース],ループ回数).Single();
                if(較正ジョブ.状態==ジョブ状態.保留) {
                    var ウォームアップ失敗 = await this.必要ならウォームアップを実行Async(状態,較正ケース,行.ソースタブ,論理CPU,要求,ウォームアップ済みケース一覧,状態変更時,キャンセルトークン);
                    if(ウォームアップ失敗 is not null) {
                        this.ウォームアップ失敗を記録(状態,較正ケース,ループ回数,論理CPU,ウォームアップ失敗,進行通知);
                        return;
                    }

                    状態変更時($"測定中 {較正ジョブ.メソッド名} {軸状態文を作成(較正ジョブ)}: {較正ジョブ.プロジェクト} / CPU {論理CPU}");
                    await this.ジョブを実行Async(状態,較正ジョブ,論理CPU,要求.ジョブタイムアウト,進行通知,キャンセルトークン);
                }

                if(較正ジョブ.状態!=ジョブ状態.完了) {
                    return;
                }
                if(!較正完了か(較正ジョブ,状態.適応しきい値ナノ秒)) {
                    if(ループ回数==int.MaxValue) {
                        this.目標時間到達不能を記録(状態,[較正ジョブ],進行通知);
                        return;
                    }
                    continue;
                }
            }

            foreach(var ベンチマーク in 同期ケース一覧) {
                var ジョブ = this.適応ジョブ一覧を取得または作成(状態,[ベンチマーク],ループ回数).Single();
                if(ジョブ.状態==ジョブ状態.失敗) {
                    return;
                }

                // 過去パスの較正がジッターで別の軸数に確定していた場合、その軸数で完了した
                // 同期ジョブが残留し、表示側の左右軸数比較が永久に不一致になる。
                // 確定軸数以外の同期ジョブはここで破棄し、常に較正結果と同じ軸数だけを残す。
                this.同期ケースの他軸ジョブを破棄(状態,ベンチマーク,ループ回数);
                if(ジョブ.状態==ジョブ状態.完了) {
                    continue;
                }

                var ウォームアップ失敗 = await this.必要ならウォームアップを実行Async(状態,ベンチマーク,行.ソースタブ,論理CPU,要求,ウォームアップ済みケース一覧,状態変更時,キャンセルトークン);
                if(ウォームアップ失敗 is not null) {
                    this.ウォームアップ失敗を記録(状態,ベンチマーク,ループ回数,論理CPU,ウォームアップ失敗,進行通知);
                    return;
                }

                状態変更時($"測定中 {ジョブ.メソッド名} {軸状態文を作成(ジョブ)}: {ジョブ.プロジェクト} / CPU {論理CPU}");
                await this.ジョブを実行Async(状態,ジョブ,論理CPU,要求.ジョブタイムアウト,進行通知,キャンセルトークン);
                if(ジョブ.状態!=ジョブ状態.完了) {
                    return;
                }
            }
            return;
        }
    }

    private async Task AOTで行を実行Async(
        進行状態 状態,
        適応メソッド行 行,
        int 論理CPU,
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        foreach(var ベンチマーク in 行.ケース一覧) {
            キャンセルトークン.ThrowIfCancellationRequested();
            var ジョブ = this.既存ケースジョブを取得(状態,ベンチマーク);
            if(ジョブ is { 状態: ジョブ状態.失敗 or ジョブ状態.完了 }) {
                continue;
            }

            ジョブ??=this.ケースジョブを取得または作成(状態,ベンチマーク,ベンチマーク作業軸.既定ループ回数);
            状態変更時($"AOT測定中 {ジョブ.メソッド名}: {ジョブ.プロジェクト} / CPU {論理CPU}");
            await this.ジョブを実行Async(状態,ジョブ,論理CPU,要求.ジョブタイムアウト,進行通知,キャンセルトークン);
            if(ジョブ.状態!=ジョブ状態.完了) {
                return;
            }
        }
    }

    private async Task 開放ループ較正で行を実行Async(
        進行状態 状態,
        適応メソッド行 行,
        ベンチマークケース 較正ケース,
        IReadOnlyList<ベンチマークケース> 同期ケース一覧,
        int 論理CPU,
        ベンチマーク実行要求 要求,
        global::System.Collections.Generic.HashSet<string> ウォームアップ済みケース一覧,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        キャンセルトークン.ThrowIfCancellationRequested();
        var 較正ジョブ = this.既存ケースジョブを取得(状態,較正ケース);
        if(較正ジョブ is { 状態: ジョブ状態.失敗 }) {
            return;
        }

        if(較正ジョブ is { 状態: ジョブ状態.完了 }&&!較正完了か(較正ジョブ,状態.適応しきい値ナノ秒)) {
            // 旧アルゴリズム(倍々探索)時代に保存された、閾値未達のまま完了扱いになっているジョブを再計測させる。
            較正ジョブ.状態=ジョブ状態.保留;
        }

        較正ジョブ??=this.ケースジョブを取得または作成(状態,較正ケース,ベンチマーク作業軸.既定ループ回数);
        if(較正ジョブ.状態==ジョブ状態.保留) {
            較正ジョブ.開放ループ較正か=true;
            較正ジョブ.較正閾値ナノ秒=状態.適応しきい値ナノ秒;
            var ウォームアップ失敗 = await this.必要ならウォームアップを実行Async(状態,較正ケース,行.ソースタブ,論理CPU,要求,ウォームアップ済みケース一覧,状態変更時,キャンセルトークン);
            if(ウォームアップ失敗 is not null) {
                this.ウォームアップ失敗を記録(状態,較正ケース,ベンチマーク作業軸.既定ループ回数,論理CPU,ウォームアップ失敗,進行通知);
                return;
            }

            状態変更時($"測定中 {較正ジョブ.メソッド名} {軸状態文を作成(較正ジョブ)}: {較正ジョブ.プロジェクト} / CPU {論理CPU}");
            await this.ジョブを実行Async(状態,較正ジョブ,論理CPU,要求.ジョブタイムアウト,進行通知,キャンセルトークン);
        }

        if(較正ジョブ.状態!=ジョブ状態.完了) {
            return;
        }

        var ループ回数 = 較正ジョブ.ループ回数;
        foreach(var ベンチマーク in 同期ケース一覧) {
            var ジョブ = this.ケースジョブを取得または作成(状態,ベンチマーク,ループ回数);
            if(ジョブ.状態==ジョブ状態.失敗) {
                return;
            }
            if(ジョブ.状態==ジョブ状態.完了&&ジョブ.ループ回数!=ループ回数) {
                // 較正側の実測ループ回数がこのパスで変わった場合、同期側も現在のループ回数で測り直す。
                ジョブ.状態=ジョブ状態.保留;
            }
            if(ジョブ.状態==ジョブ状態.完了) {
                continue;
            }

            ジョブ.ループ回数=ループ回数;
            var ウォームアップ失敗 = await this.必要ならウォームアップを実行Async(状態,ベンチマーク,行.ソースタブ,論理CPU,要求,ウォームアップ済みケース一覧,状態変更時,キャンセルトークン);
            if(ウォームアップ失敗 is not null) {
                this.ウォームアップ失敗を記録(状態,ベンチマーク,ループ回数,論理CPU,ウォームアップ失敗,進行通知);
                return;
            }

            状態変更時($"測定中 {ジョブ.メソッド名} {軸状態文を作成(ジョブ)}: {ジョブ.プロジェクト} / CPU {論理CPU}");
            await this.ジョブを実行Async(状態,ジョブ,論理CPU,要求.ジョブタイムアウト,進行通知,キャンセルトークン);
            if(ジョブ.状態!=ジョブ状態.完了) {
                return;
            }
        }
    }

    private async Task<string?> 必要ならウォームアップを実行Async(進行状態 状態,ベンチマークケース ベンチマーク,ベンチマークソースタブ ソースタブ,int 論理CPU,ベンチマーク実行要求 要求,global::System.Collections.Generic.HashSet<string> ウォームアップ済みケース一覧,Action<string> 状態変更時,CancellationToken キャンセルトークン) {
        if(!ウォームアップを実行するか(要求.実行プロファイル)||!ウォームアップ済みケース一覧.Add(ベンチマークキーを作成(ベンチマーク))) {
            return null;
        }

        var 失敗=await this.ウォームアップケースを実行Async(状態,ベンチマーク,ソースタブ,論理CPU,要求,状態変更時,キャンセルトークン);
        return 失敗?.失敗理由;
    }

    private static bool 較正完了か(ベンチマークジョブ? ジョブ,double 閾値ナノ秒) =>
        ジョブ is { 状態: ジョブ状態.完了, 平均ナノ秒: { } 合計 }&&合計>=閾値ナノ秒;

    private async Task スケーラブル行を実行Async(
        進行状態 状態,
        適応メソッド行 行,
        int 論理CPU,
        ベンチマーク実行要求 要求,
        Action<ベンチマーク進行更新> 進行通知,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        foreach(var ベンチマーク in 行.ケース一覧) {
            キャンセルトークン.ThrowIfCancellationRequested();
            var ウォームアップ済みか = false;
            foreach(var 要素数 in 適応要素数を列挙(状態,行)) {
                キャンセルトークン.ThrowIfCancellationRequested();
                var ベンチマークケース = 適応ケースを解決(行,ベンチマーク,要素数);
                if(ベンチマークケース is null) {
                    continue;
                }

                if(this.適応ケースが停止済みか(状態,行.ソースタブ,ベンチマークケース)) {
                    break;
                }

                var 既存ジョブ一覧 = this.既存適応ジョブ一覧を取得(状態,[ベンチマークケース],要素数);
                if(既存ジョブ一覧.Count>0&&既存ジョブ一覧.All(static ジョブ => ジョブ.状態!=ジョブ状態.保留)) {
                    continue;
                }

                if(!ウォームアップ済みか&&!スケーラブルソースタブか(行.ソースタブ)&&ウォームアップを実行するか(要求.実行プロファイル)) {
                    ウォームアップ済みか=true;
                    var ウォームアップ失敗 = await this.ウォームアップケースを実行Async(
                        状態,
                        ベンチマークケース,
                        行.ソースタブ,
                        論理CPU,
                        要求,
                        状態変更時,
                        キャンセルトークン);
                    if(ウォームアップ失敗 is not null) {
                        this.ウォームアップ失敗を記録(
                            状態,
                            ウォームアップ失敗.ベンチマーク,
                            要素数,
                            論理CPU,
                            ウォームアップ失敗.失敗理由,
                            進行通知);
                        break;
                    }
                }

                var ジョブ一覧 = this.適応ジョブ一覧を取得または作成(状態,[ベンチマークケース],要素数);
                var ジョブ = ジョブ一覧.FirstOrDefault(static ジョブ => ジョブ.状態==ジョブ状態.保留);
                if(ジョブ is null) {
                    continue;
                }

                状態変更時($"実行中 {ジョブ.メソッド名} {軸状態文を作成(ジョブ)}: {ジョブ.プロジェクト} / CPU {論理CPU}");
                await this.ジョブを実行Async(状態,ジョブ,論理CPU,要求.ジョブタイムアウト,進行通知,キャンセルトークン);
                if(ジョブ.状態 is ジョブ状態.保留 or ジョブ状態.実行中) {
                    break;
                }
            }
        }
    }

    private static IEnumerable<int> 適応ループ回数を列挙(進行状態 状態) {
        var 初期値一覧 = 状態.要素数一覧
            .Where(static 値 => 値>0)
            .Distinct()
            .Order()
            .ToArray();
        var 最後の値 = 0;
        foreach(var 値 in 初期値一覧) {
            最後の値=値;
            yield return 値;
        }

        while(最後の値>0&&最後の値<int.MaxValue) {
            最後の値=最後の値>int.MaxValue/2 ? int.MaxValue : 最後の値*2;
            yield return 最後の値;
        }
    }

    private static ベンチマークケース? 適応ケースを解決(適応メソッド行 行,ベンチマークケース ベンチマーク,int 要素数) {
        if(!スケーラブルソースタブか(行.ソースタブ)) {
            return ベンチマーク;
        }

        if(ベンチマーク.Count引数==要素数) {
            return ベンチマーク;
        }

        return ベンチマーク.Count引数>0
            ? Count引数を変更して複製(ベンチマーク,要素数)
            : null;
    }

    private static IEnumerable<int> 適応要素数を列挙(進行状態 状態,適応メソッド行 行) {
        var 初期値一覧 = 適応要素数一覧を取得(状態,行)
            .Where(static 値 => 値>0)
            .Distinct()
            .Order()
            .ToArray();
        var 最後の値 = 0;
        foreach(var 値 in 初期値一覧) {
            最後の値=値;
            yield return 値;
        }

        if(最後の値!=ベンチマーク要素数一覧.全て[^1]) {
            yield break;
        }

        while(最後の値>0&&最後の値<int.MaxValue) {
            if(最後の値>int.MaxValue/2) {
                最後の値=int.MaxValue;
            } else {
                最後の値*=2;
            }

            yield return 最後の値;
        }
    }

    private static IReadOnlyList<int> 適応要素数一覧を取得(進行状態 状態,適応メソッド行 行) {
        if(!スケーラブルソースタブか(行.ソースタブ)) {
            return 状態.要素数一覧;
        }

        var 値一覧 = 行.ケース一覧
            .Select(static ベンチマーク => ベンチマーク.Count引数)
            .Where(static 値 => 値>0)
            .Distinct()
            .Order()
            .ToArray();
        return 値一覧.Length>0 ? 値一覧 : 状態.要素数一覧;
    }

    private static ベンチマークケース Count引数を変更して複製(ベンチマークケース ベンチマーク,int 要素数) {
        var 引数一覧 = ベンチマーク.引数一覧
            .Select(引数=>string.Equals(引数.名前,"Count",StringComparison.OrdinalIgnoreCase)
                ? new ベンチマーク引数(引数.名前,要素数.ToString(CultureInfo.InvariantCulture))
                : 引数)
            .ToArray();
        return new ベンチマークケース(
            ベンチマーク.プロジェクト,
            ベンチマーク.アセンブリ名,
            ベンチマーク.型完全名,
            ベンチマーク.メソッド名,
            ベンチマーク引数フォーマッター.キーを作成(引数一覧),
            引数一覧) {
            バインド行インデックス=ベンチマーク.バインド行インデックス,
        };
    }

    private async Task<ウォームアップ失敗?> ウォームアップケースを実行Async(
        進行状態 状態,
        ベンチマークケース ベンチマーク,
        ベンチマークソースタブ ソースタブ,
        int 論理CPU,
        ベンチマーク実行要求 要求,
        Action<string> 状態変更時,
        CancellationToken キャンセルトークン) {
        キャンセルトークン.ThrowIfCancellationRequested();
        var ウォームアップジョブ = ジョブを作成(ベンチマーク,1,ソースタブ,状態.要素数一覧,-1);
        ウォームアップジョブ.ジョブID=ウォームアップジョブ.ジョブID+"|warmup";
        状態変更時($"ウォームアップ {ベンチマーク.メソッド名} "+軸状態文を作成(ウォームアップジョブ)+$": {ベンチマーク.プロジェクト} / CPU {論理CPU}");
        try {
            var 結果 = await ジョブ実行器.実行Async(
                ウォームアップジョブ,
                状態.実行ルート,
                論理CPU,
                要求.ジョブタイムアウト,
                キャンセルトークン);
            if(!測定完了か(結果)) {
                return new ウォームアップ失敗(
                    ベンチマーク,
                    最初の文字列を取得(
                        結果.ログ,
                        ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了)
                            ? "ウォームアップは完了しましたが、ベンチマーク統計がありません。"
                            : $"ウォームアップが状態 {結果.状態} を返しました。"));
            }

            ディレクトリ削除を試みる(結果.成果物フォルダー);
        } catch(Exception 例外) when(例外 is not OperationCanceledException) {
            return new ウォームアップ失敗(ベンチマーク,例外.ToString());
        }

        return null;
    }

    private async Task ジョブを実行Async(
        進行状態 状態,
        ベンチマークジョブ ジョブ,
        int 論理CPU,
        TimeSpan タイムアウト,
        Action<ベンチマーク進行更新> 進行通知,
        CancellationToken キャンセルトークン) {
        ジョブ.状態=ジョブ状態.実行中;
        ジョブ.割り当て論理CPU=論理CPU;
        ジョブ.開始日時=DateTimeOffset.UtcNow;
        ジョブ.終了日時=null;
        ジョブ.失敗理由=string.Empty;
        状態マネージャー.保存(状態);
        進行通知(new ベンチマーク進行更新(状態,ジョブ));

        try {
            System.Diagnostics.Debug.WriteLine($"[オーケストレーター] ジョブを実行Async 開始 CPU={論理CPU} メソッド={ジョブ.メソッド名} 要素数={ジョブ.要素数}");
            var ジョブストップウォッチ = Stopwatch.StartNew();
            var 結果 = await ジョブ実行器.実行Async(ジョブ,状態.実行ルート,論理CPU,タイムアウト,キャンセルトークン);
            ジョブストップウォッチ.Stop();
            System.Diagnostics.Debug.WriteLine($"[オーケストレーター] ジョブを実行Async 終了 CPU={論理CPU} メソッド={ジョブ.メソッド名} 状態={結果.状態} 経過時間={ジョブストップウォッチ.Elapsed.TotalMilliseconds:0.0}ms");
            var 完了か = ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了)
                &&結果.平均ナノ秒 is not null;
            ジョブ.状態=完了か
                ? ジョブ状態.完了
                : ジョブ状態.失敗;
            ジョブ.平均=結果.平均;
            if(結果.ループ回数>0) {
                ジョブ.ループ回数=結果.ループ回数;
            }
            ジョブ.誤差=結果.誤差;
            ジョブ.標準偏差=結果.標準偏差;
            ジョブ.平均ナノ秒=結果.平均ナノ秒;
            ジョブ.誤差ナノ秒=結果.誤差ナノ秒;
            ジョブ.標準偏差ナノ秒=結果.標準偏差ナノ秒;
            ジョブ.割り当てバイト数=結果.割り当てバイト数;
            ジョブ.乱数シード=結果.乱数シード;
            ジョブ.結果ダイジェスト=結果.結果ダイジェスト;
            ジョブ.診断カウンター一覧=結果.診断カウンター一覧.ToList();
            ジョブ.終了コード=結果.終了コード;
            ジョブ.成果物フォルダー=結果.成果物フォルダー;
            if(ジョブ.状態==ジョブ状態.失敗) {
                ジョブ.失敗理由=最初の文字列を取得(
                    結果.ログ,
                    ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了)
                        ? "ワーカーは完了しましたが、ベンチマーク統計がありません。"
                        : $"ワーカーが状態 {結果.状態} を返しました。");
            }
        } catch(OperationCanceledException) when(キャンセルトークン.IsCancellationRequested) {
            ジョブ.状態=ジョブ状態.保留;
            ジョブ.割り当て論理CPU=null;
            ジョブ.開始日時=null;
            throw;
        } catch(Exception 例外) {
            ジョブ.状態=ジョブ状態.失敗;
            ジョブ.終了コード=1;
            ジョブ.失敗理由=例外.ToString();
        } finally {
            if(ジョブ.状態!=ジョブ状態.保留) {
                ジョブ.終了日時=DateTimeOffset.UtcNow;
            }
            状態マネージャー.保存(状態);
            進行通知(new ベンチマーク進行更新(状態,ジョブ));
        }
    }

    private 進行状態 状態を作成(ベンチマーク実行要求 要求) {
        var 現在日時 = DateTimeOffset.UtcNow;
        var 実行ルート = string.IsNullOrWhiteSpace(要求.実行ルート上書き)
            ? this.次の実行ルートを作成()
            : 要求.実行ルート上書き;
        var 状態 = new 進行状態 {
            実行ID=Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            実行プロファイル=要求.実行プロファイル,
            実行ルート=実行ルート,
            実行番号=ベンチマーク状態マネージャー.実行番号の解析を試みる(実行ルート),
            ソースタブ=要求.ソースタブ,
            要素数一覧=要求.要素数一覧.Distinct().Order().ToList(),
            適応しきい値ナノ秒=要求.適応しきい値.TotalMilliseconds*1_000_000,
            作成日時=現在日時,
            更新日時=現在日時,
        };
        if(状態.要素数一覧.Count==0) {
            状態.要素数一覧=ベンチマーク要素数一覧.既定初期値.ToList();
        }

        状態.適応状態一覧=this.適応行一覧を作成(状態,要求)
            .Select(static 行=>行.キー)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static キー => キー,StringComparer.OrdinalIgnoreCase)
            .Select(static キー => new 適応ベンチマーク状態 { メソッド名=キー })
            .ToList();
        return 状態;
    }

    private string 次の実行ルートを作成() => 状態マネージャー.次の実行ルートを作成();

    private void 状態を要求と同期(進行状態 状態,ベンチマーク実行要求 要求) {
        if(状態.実行番号<=0) {
            状態.実行番号=ベンチマーク状態マネージャー.実行番号の解析を試みる(状態.実行ルート);
        }
        状態.実行プロファイル=要求.実行プロファイル;
        状態.ソースタブ=要求.ソースタブ;
        状態.適応しきい値ナノ秒=要求.適応しきい値.TotalMilliseconds*1_000_000;
        状態.要素数一覧=要求.要素数一覧.Distinct().Order().ToList();
        if(状態.要素数一覧.Count==0) {
            状態.要素数一覧=ベンチマーク要素数一覧.既定初期値.ToList();
        }
        if(状態.ジョブ一覧.Count>0) {
            状態.ジョブ一覧=状態.ジョブ一覧
                .Where(ジョブ => 要求.ベンチマーク一覧.Any(ベンチマーク => 同じケースか(ベンチマーク,ジョブ)))
                .ToList();
        }

        if(要求.要求要素数から再開するか&&状態.要素数一覧.Count>0) {
            指定要素数以降のジョブをリセット(状態,状態.要素数一覧[0]);
        }

        var 要求メソッド一覧 = this.適応行一覧を作成(状態,要求)
            .Select(static 行=>行.キー)
            .ToHashSet(StringComparer.Ordinal);
        状態.適応状態一覧=状態.適応状態一覧
            .Where(適応状態 => 要求メソッド一覧.Contains(適応状態.メソッド名))
            .ToList();
        foreach(var キー in 要求メソッド一覧.Where(キー => 状態.適応状態一覧.All(項目 => !string.Equals(項目.メソッド名,キー,StringComparison.Ordinal)))) {
            状態.適応状態一覧.Add(new 適応ベンチマーク状態 { メソッド名=キー });
        }
    }

    private static void 指定要素数以降のジョブをリセット(進行状態 状態,int 要素数) {
        foreach(var ジョブ in 状態.ジョブ一覧.Where(ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)>=要素数)) {
            ジョブ.状態=ジョブ状態.保留;
            ジョブ.割り当て論理CPU=null;
            ジョブ.開始日時=null;
            ジョブ.終了日時=null;
            ジョブ.失敗理由=string.Empty;
            ジョブ.平均=string.Empty;
            ジョブ.誤差=string.Empty;
            ジョブ.標準偏差=string.Empty;
            ジョブ.平均ナノ秒=null;
            ジョブ.誤差ナノ秒=null;
            ジョブ.標準偏差ナノ秒=null;
            ジョブ.割り当てバイト数=null;
            ジョブ.終了コード=0;
            ジョブ.成果物フォルダー=string.Empty;
        }
    }

    private IReadOnlyList<ベンチマークジョブ>適応ジョブ一覧を取得または作成(進行状態 状態,IReadOnlyList<ベンチマークケース>ケース一覧,int 要素数) {
        lock(this.状態排他) {
            var 既存一覧 = 状態.ジョブ一覧
                .Where(ジョブ=>ベンチマーク作業軸.軸数を取得(ジョブ)==要素数&&ケース一覧.Any(項目=>同じケースか(項目,ジョブ)))
                .ToArray();
            if(既存一覧.Length==ケース一覧.Count) {
                return 既存一覧;
            }

            foreach(var ベンチマーク in ケース一覧) {
                if(状態.ジョブ一覧.Any(ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)==要素数&&同じケースか(ベンチマーク,ジョブ))) {
                    continue;
                }

                状態.ジョブ一覧.Add(ジョブを作成(ベンチマーク,要素数,ベンチマークソースタブを取得(ベンチマーク,状態.ソースタブ),状態.要素数一覧,状態.次の実行項目インデックス++));
            }

            状態マネージャー.保存(状態);
            return 状態.ジョブ一覧
                .Where(ジョブ=>ベンチマーク作業軸.軸数を取得(ジョブ)==要素数&&ケース一覧.Any(項目=>同じケースか(項目,ジョブ)))
                .ToArray();
        }
    }

    private IReadOnlyList<ベンチマークジョブ>既存適応ジョブ一覧を取得(進行状態 状態,IReadOnlyList<ベンチマークケース>ケース一覧,int 要素数) {
        lock(this.状態排他) {
            return 状態.ジョブ一覧
                .Where(ジョブ=>ベンチマーク作業軸.軸数を取得(ジョブ)==要素数&&ケース一覧.Any(項目=>同じケースか(項目,ジョブ)))
                .ToArray();
        }
    }

    private void 同期ケースの他軸ジョブを破棄(進行状態 状態,ベンチマークケース ケース,int 確定軸数) {
        lock(this.状態排他) {
            var 破棄件数 = 状態.ジョブ一覧.RemoveAll(ジョブ =>
                同じケースか(ケース,ジョブ)
                &&ベンチマーク作業軸.軸数を取得(ジョブ)!=確定軸数);
            if(破棄件数>0) {
                状態マネージャー.保存(状態);
            }
        }
    }

    // 開放ループ較正(単発プローブ)では較正ケース・同期ケースとも行あたり常に単一のジョブしか
    // 存在しないため、軸数(実測で変わりうる)ではなくケース識別だけで一意に特定する。
    private ベンチマークジョブ? 既存ケースジョブを取得(進行状態 状態,ベンチマークケース ケース) {
        lock(this.状態排他) {
            return 状態.ジョブ一覧.FirstOrDefault(ジョブ => 同じケースか(ケース,ジョブ));
        }
    }

    private ベンチマークジョブ ケースジョブを取得または作成(進行状態 状態,ベンチマークケース ケース,int 初期ループ回数) {
        lock(this.状態排他) {
            var 既存 = 状態.ジョブ一覧.FirstOrDefault(ジョブ => 同じケースか(ケース,ジョブ));
            if(既存 is not null) {
                return 既存;
            }

            var 新規 = ジョブを作成(ケース,初期ループ回数,ベンチマークソースタブを取得(ケース,状態.ソースタブ),状態.要素数一覧,状態.次の実行項目インデックス++);
            状態.ジョブ一覧.Add(新規);
            状態マネージャー.保存(状態);
            return 新規;
        }
    }

    private void ウォームアップ失敗を記録(
        進行状態 状態,
        ベンチマークケース ベンチマーク,
        int 要素数,
        int 論理CPU,
        string 失敗理由,
        Action<ベンチマーク進行更新> 進行通知) {
        ベンチマークジョブ ジョブ;
        lock(this.状態排他) {
            ジョブ=状態.ジョブ一覧.FirstOrDefault(項目 => ベンチマーク作業軸.軸数を取得(項目)==要素数&&同じケースか(ベンチマーク,項目))
                ??ジョブを作成(ベンチマーク,要素数,ベンチマークソースタブを取得(ベンチマーク,状態.ソースタブ),状態.要素数一覧,状態.次の実行項目インデックス++);
            if(!状態.ジョブ一覧.Contains(ジョブ)) {
                状態.ジョブ一覧.Add(ジョブ);
            }

            ジョブ.状態=ジョブ状態.失敗;
            ジョブ.割り当て論理CPU=論理CPU;
            ジョブ.開始日時=DateTimeOffset.UtcNow;
            ジョブ.終了日時=DateTimeOffset.UtcNow;
            ジョブ.平均="ウォームアップ失敗";
            ジョブ.誤差=string.Empty;
            ジョブ.標準偏差=string.Empty;
            ジョブ.平均ナノ秒=null;
            ジョブ.誤差ナノ秒=null;
            ジョブ.標準偏差ナノ秒=null;
            ジョブ.割り当てバイト数=null;
            ジョブ.終了コード=1;
            ジョブ.失敗理由=最初の文字列を取得(失敗理由,"ウォームアップに失敗しました。");
            状態マネージャー.保存(状態);
        }

        進行通知(new ベンチマーク進行更新(状態,ジョブ));
    }

    private static ベンチマークジョブ ジョブを作成(
        ベンチマークケース ベンチマーク,
        int 要素数,
        ベンチマークソースタブ ソースタブ,
        IEnumerable<int> 準備済み要素数一覧,
        int 実行項目インデックス) =>
        new() {
            ジョブID=ジョブIDを作成(ベンチマーク,ソースタブ,要素数),
            プロジェクト=ベンチマーク.プロジェクト,
            アセンブリ名=ベンチマーク.アセンブリ名,
            型完全名=ベンチマーク.型完全名,
            メソッド名=ベンチマーク.メソッド名,
            引数キー=ベンチマーク.引数キー,
            引数一覧=ベンチマーク.引数一覧.ToList(),
            ソースタブ=ソースタブ,
            要素数=ベンチマーク作業軸.要素数を解決(ソースタブ,要素数),
            ループ回数=ベンチマーク作業軸.ループ回数を解決(ソースタブ,要素数),
            準備済み要素数一覧=ベンチマーク作業軸.準備済み要素数を解決(ソースタブ,準備済み要素数一覧).ToList(),
            実行項目インデックス=実行項目インデックス,
            バインド行インデックス=ベンチマーク.バインド行インデックス,
        };

    private static string ジョブIDを作成(ベンチマークケース ベンチマーク,ベンチマークソースタブ ソースタブ,int 軸数)=>
        string.Join("|",ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー,ベンチマーク作業軸.要素数を解決(ソースタブ,軸数),ベンチマーク作業軸.ループ回数を解決(ソースタブ,軸数));

    private static string ベンチマークキーを作成(ベンチマークケース ベンチマーク) =>
        string.Join("|",ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー);

    private void 現在の適応行をキューへ追加(
        進行状態 状態,
        ConcurrentQueue<適応メソッド行> キュー,
        global::System.Collections.Generic.HashSet<string> キュー済みキー一覧,
        global::System.Collections.Generic.HashSet<string> 完了キー一覧,
        ベンチマーク実行要求 要求) {
        foreach(var 行 in this.適応行一覧を作成(状態,要求)) {
            lock(キュー済みキー一覧) {
                if(キュー済みキー一覧.Contains(行.キー)||完了キー一覧.Contains(行.キー)) {
                    continue;
                }

                キュー済みキー一覧.Add(行.キー);
                キュー.Enqueue(行);
            }
        }
    }

    private bool 現在の適応行取得を試みる(
        進行状態 状態,
        ConcurrentQueue<適応メソッド行> キュー,
        global::System.Collections.Generic.HashSet<string> キュー済みキー一覧,
        ベンチマーク実行要求 要求,
        out 適応メソッド行 行) {
        while(キュー.TryDequeue(out var 候補)) {
            var 現在 = this.適応行一覧を作成(状態,要求)
                .FirstOrDefault(項目=>string.Equals(項目.キー,候補.キー,StringComparison.OrdinalIgnoreCase));
            if(現在 is not null) {
                行=現在;
                return true;
            }

            lock(キュー済みキー一覧) {
                キュー済みキー一覧.Remove(候補.キー);
            }
        }

        行=null!;
        return false;
    }

    private IReadOnlyList<適応メソッド行> 適応行一覧を作成(進行状態 状態,ベンチマーク実行要求 要求) {
        var ジョブ一覧 = this.ジョブスナップショットを取得(状態);
        var ベンチマーク一覧 = (要求.ベンチマーク取得器?.Invoke()??要求.ベンチマーク一覧)
            .Concat(ジョブ一覧.Select(static ジョブ => ジョブ.ベンチマークケースに変換()))
            .Where(static ベンチマーク => !string.IsNullOrWhiteSpace(ベンチマーク.型完全名))
            .GroupBy(ベンチマークキーを作成,StringComparer.Ordinal)
            .Select(static グループ => グループ.First())
            .ToArray();
        return ベンチマーク一覧
            .Select((ベンチマーク,インデックス) => new { ベンチマーク,インデックス })
            .GroupBy(項目 => ベンチマークソースタブを取得(項目.ベンチマーク,要求.ソースタブ))
            .OrderBy(static グループ => グループ.Min(static 項目 => 項目.インデックス))
            .SelectMany(グループ => {
                var ソースタブ = ベンチマークソースタブ一覧.取得(グループ.Key);
                return ベンチマーク行グループ化ロジック.行を作成(グループ.OrderBy(static 項目 => 項目.インデックス).Select(static 項目 => 項目.ベンチマーク).ToArray(),ソースタブ)
                    .Select(行 => new 適応メソッド行(
                        行.行キー,
                        ソースタブ.タブ,
                        行.メソッド名,
                        ソースタブ.プロジェクト順
                            .Where(プロジェクト=>行.プロジェクト別ケース.ContainsKey(プロジェクト))
                            .Select(プロジェクト=>行.プロジェクト別ケース[プロジェクト])
                            .ToArray()));
            })
            .ToArray();
    }

    private static ベンチマークソースタブ ベンチマークソースタブを取得(ベンチマークケース ベンチマーク,ベンチマークソースタブ 代替ソースタブ) =>
        ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名)?.タブ??代替ソースタブ;

    private static bool 同じケースか(ベンチマークケース ベンチマーク,ベンチマークジョブ ジョブ) =>
        string.Equals(ベンチマーク.プロジェクト,ジョブ.プロジェクト,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(ベンチマーク.アセンブリ名,ジョブ.アセンブリ名,StringComparison.Ordinal)
        &&string.Equals(ベンチマーク.型完全名,ジョブ.型完全名,StringComparison.Ordinal)
        &&string.Equals(ベンチマーク.メソッド名,ジョブ.メソッド名,StringComparison.Ordinal)
        &&string.Equals(ベンチマーク.引数キー,ジョブ.引数キー,StringComparison.Ordinal);

    private void 目標時間到達不能を記録(
        進行状態 状態,
        IReadOnlyList<ベンチマークジョブ> ジョブ一覧,
        Action<ベンチマーク進行更新> 進行通知) {
        const string 理由 = "ループ回数がint.MaxValueに達しても目標測定時間へ到達できませんでした。";
        ベンチマークジョブ[] 失敗ジョブ一覧;
        lock(this.状態排他) {
            失敗ジョブ一覧=ジョブ一覧.Where(static ジョブ => ジョブ.状態==ジョブ状態.完了).ToArray();
            foreach(var ジョブ in 失敗ジョブ一覧) {
                ジョブ.状態=ジョブ状態.失敗;
                ジョブ.終了コード=1;
                ジョブ.失敗理由=理由;
            }
            状態マネージャー.保存(状態);
        }

        foreach(var ジョブ in 失敗ジョブ一覧) {
            進行通知(new ベンチマーク進行更新(状態,ジョブ));
        }
    }

    private bool 適応ケースが停止済みか(進行状態 状態,ベンチマークソースタブ ソースタブ,ベンチマークケース ベンチマーク) {
        var ジョブ一覧 = this.ジョブスナップショットを取得(状態);
        return 適応ケースが失敗済みか(ジョブ一覧,ソースタブ,ベンチマーク)
            ||適応ケースが閾値超過か(状態,ジョブ一覧,ソースタブ,ベンチマーク);
    }

    private static bool 適応ケースが失敗済みか(IReadOnlyList<ベンチマークジョブ> ジョブ一覧,ベンチマークソースタブ ソースタブ,ベンチマークケース ベンチマーク) =>
        ジョブ一覧.Any(ジョブ => 同じ適応系列か(ソースタブ,ベンチマーク,ジョブ)&&ジョブ.状態==ジョブ状態.失敗);

    private static bool 適応ケースが閾値超過か(進行状態 状態,IReadOnlyList<ベンチマークジョブ> ジョブ一覧,ベンチマークソースタブ ソースタブ,ベンチマークケース ベンチマーク) =>
        ジョブ一覧.Any(ジョブ =>
            同じ適応系列か(ソースタブ,ベンチマーク,ジョブ)
            &&ジョブ.状態==ジョブ状態.完了
            &&ジョブ.平均ナノ秒 is { } 平均
            &&平均>=状態.適応しきい値ナノ秒);

    private static bool 同じ適応系列か(ベンチマークソースタブ ソースタブ,ベンチマークケース ベンチマーク,ベンチマークジョブ ジョブ) {
        if(!スケーラブルソースタブか(ソースタブ)) {
            return 同じケースか(ベンチマーク,ジョブ);
        }

        return string.Equals(ベンチマーク.プロジェクト,ジョブ.プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(ベンチマーク.アセンブリ名,ジョブ.アセンブリ名,StringComparison.Ordinal)
            &&string.Equals(ベンチマーク.型完全名,ジョブ.型完全名,StringComparison.Ordinal)
            &&string.Equals(ベンチマーク.メソッド名,ジョブ.メソッド名,StringComparison.Ordinal);
    }

    private IReadOnlyList<ベンチマークジョブ> ジョブスナップショットを取得(進行状態 状態) {
        lock(this.状態排他) {
            return 状態.ジョブ一覧.ToArray();
        }
    }

    private static bool スケーラブルソースタブか(ベンチマークソースタブ ソースタブ) =>
        ベンチマーク作業軸.スケーラブルソースタブか(ソースタブ);

    private static string 軸状態文を作成(ベンチマークジョブ ジョブ) =>
        ベンチマーク作業軸.ループ回数を使うか(ジョブ.ソースタブ)
            ? "ループ回数="+ジョブ.ループ回数.ToString(CultureInfo.InvariantCulture)+" / 要素数="+ジョブ.要素数.ToString(CultureInfo.InvariantCulture)
            : "要素数="+ジョブ.要素数.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<int> CPU一覧を正規化(IReadOnlyList<int> CPU一覧) =>
        CPU一覧.Distinct().Order().ToArray();

    private static IReadOnlyList<int> 現在CPU一覧を取得(ベンチマーク実行要求 要求) =>
        CPU一覧を正規化(要求.論理CPU取得器?.Invoke()??要求.論理CPU一覧);

    private static bool ウォームアップを実行するか(ベンチマーク実行プロファイル 実行プロファイル) =>
        実行プロファイル is ベンチマーク実行プロファイル.高速 or ベンチマーク実行プロファイル.中間;

    private static bool 測定完了か(ベンチマークジョブ実行結果 結果) =>
        string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
        &&結果.平均ナノ秒 is not null;

    private static void ディレクトリ削除を試みる(string パス) {
        try {
            if(!string.IsNullOrWhiteSpace(パス)&&Directory.Exists(パス)) {
                Directory.Delete(パス,recursive: true);
            }
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }

    private static string 最初の文字列を取得(params string[] 値一覧) =>
        値一覧.FirstOrDefault(static 値 => !string.IsNullOrWhiteSpace(値))??string.Empty;

    private sealed record 適応メソッド行(
        string キー,
        ベンチマークソースタブ ソースタブ,
        string メソッド名,
        IReadOnlyList<ベンチマークケース> ケース一覧);

    private sealed record 適応ワーカー(string 行キー,Task タスク) {
        public bool 完了か=>this.タスク.IsCompleted;
    }

    private sealed record ウォームアップ失敗(
        ベンチマークケース ベンチマーク,
        string 失敗理由);
}

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク要素数一覧 {
#if SCALABLE_BENCHMARK
    private const int 基本要素数指数=16;
    private const int 最初要素数指数=16;
    private const int 要素数指数個数=8;
#else
    private const int 基本要素数指数=12;
#endif
    internal const int 基本要素数=1<<基本要素数指数;

#if SCALABLE_BENCHMARK
    public static IReadOnlyList<int>全て{get;}=
        Enumerable.Range(最初要素数指数,要素数指数個数).Select(static 指数=>1<<指数).ToArray();

    public static IReadOnlyList<int>既定初期値{get;}=
        [基本要素数];
#else
    public static IReadOnlyList<int>全て{get;}=
        [ベンチマーク作業軸.既定ループ回数];

    public static IReadOnlyList<int>既定初期値{get;}=
        [ベンチマーク作業軸.既定ループ回数];
#endif
}
