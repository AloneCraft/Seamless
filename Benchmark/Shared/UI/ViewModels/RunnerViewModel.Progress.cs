using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// 進行状態と完了判定
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// 状態文字列値 フィールドです。
    /// </summary>
    private string 状態文字列値 = "準備完了";

    private IReadOnlyList<ベンチマーク結果項目> 直近完了結果項目一覧 = [];

    /// <summary>
    /// 実行開始時に固定する、タブ別の選択済み進行セルです。
    /// </summary>
    private IReadOnlyDictionary<ベンチマークソースタブ,IReadOnlyList<ベンチマークタブ進行セル>> 現在実行タブ別進行セル一覧 = new Dictionary<ベンチマークソースタブ,IReadOnlyList<ベンチマークタブ進行セル>>();

    /// <summary>
    /// 全体ソースタブ進行値 フィールドです。
    /// </summary>
    private ベンチマークタブ進行 全体ソースタブ進行値;

    /// <summary>
    /// 実行終了後、パイプライン排出完了時に一度だけ表示最終化を行うためのフラグです。
    /// </summary>
    private bool 実行後表示確定保留中か;

    /// <summary>
    /// 表示最終化を最終クリーンアップ実施後まで遅らせるための、フラグ設定時点のクリーンアップトークンです。
    /// </summary>
    private int 実行後確定クリア保留トークン;

    public string 全体ソース進行概要文字列 =>
        "確定 "+(this.全体ソースタブ進行.合計数<=0 ? "0/0" : this.全体ソースタブ進行.割合文字列)
        +","+this.全体ベンチマークシェア概要短文;

    /// <summary>
    /// 全タブ結果から進行状態一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行ルート">実行ルート を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<進行状態> 全タブ結果から進行状態一覧を作成(string 実行ルート) {
        var 結果一覧 = this.現在実行セッション.結果ありか
            ? this.現在実行セッション.全結果を取得()
            : this.表示用結果ソースを取得().ToArray();
        return 結果一覧
            .GroupBy(static 結果 => 結果.ソースタブ)
            .Select(グループ => this.結果から進行状態を作成(
                実行ルート,
                ベンチマークソースタブ一覧.取得(グループ.Key),
                グループ))
            .ToArray();
    }

    /// <summary>
    /// 結果から進行状態を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行ルート">実行ルート を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="ソース結果一覧">ソース結果一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private 進行状態 結果から進行状態を作成(
        string 実行ルート,
        ベンチマークソースタブ定義 ソースタブ,
        IEnumerable<ベンチマーク結果項目> ソース結果一覧) {
        this.現在ベンチマーク選択を取得();
        return ベンチマーク実行セッション.進行状態を作成(
            実行ルート,
            this.選択実行プロファイル.プロファイル,
            ソースタブ,
            this.適応閾値を取得(),
            this.ベンチマーク行選択状態,
            ソース結果一覧,
            結果 => this.ベンチマークケースを検索(結果.メソッド,結果.プロジェクト,結果.型完全名,結果.引数キー,結果.ソースタブ));
    }

    /// <summary>
    /// 実行終了後、パイプラインの持ち越し終端イベントが全て反映された時点で一度だけ
    /// 表示の最終化(値の再描画+完全完了アイコン+バッジ)を行います。
    /// 最終ステップの結果は実行終了後に描画されることがあり、実行中(実行中)分岐の
    /// 最終完了状態を更新 ではそれらを確定表示へ反映できないためです。
    /// </summary>
    private void 実行後表示確定の適用を試みる() {
        if(!this.実行後表示確定保留中か||this.ベンチマーク実行有効か||this.実行イベントパイプライン.保留ありか) {
            return;
        }
        if(this.最終ライブ表示クリアトークン==this.実行後確定クリア保留トークン) {
            // 最終クリーンアップ(遅延スケジュール分)がまだ実施されていない。クリーンアップは
            // 行ライブ状態や表示を作り直すため、先に最終化するとその成果が消される。
            // クリーンアップ末尾の呼び出しで改めて判定される。
            return;
        }

        this.実行後表示確定保留中か=false;
        起動診断.パンくずを追加(
            "実行後表示確定を適用 選択="
            +this.選択ソース定義.タブ
            +" 進行="+this.全体ソースタブ進行.割合文字列);
        起動診断.書き込む("実行後表示確定を適用しました。");
        UIスレッド予算.計測("post-run-finalize",() => {
            this.結果確認を更新(this.選択ソース定義.タブ);
            this.表示結果を更新();
            this.選択要素数を表へ適用();
            this.ソースタブ進行を同期適用();
            this.最終完了状態を同期適用();
            // 実行終了後に遅延到着した「実行中」イベントが実行中ジョブ一覧へ残ると、
            // 対応する完了イベントがもう来ないため完了アイコンが永久に隠れたままになる。
            // ここで無条件にクリアし、タブの完了アイコン表示を確定させる。
            this.ソースタブ実行中状態をクリア();
            // セル単位の実行中フラグも同様に、遅延到着した「実行中」イベントで
            // 実測値表示後に再度trueへ戻ったまま固まることがあるため、ここで強制的にクリアする。
            this.ベンチマークライブ状態をクリア();
        });
    }

    /// <summary>
    /// 最新完了ライブ結果を復元 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 最新完了ライブ結果を復元() {
        if(!this.選択タブに現在実行結果があるか) {
            return;
        }

        var 結果 = this.選択タブの現在実行結果項目一覧を取得()
            .LastOrDefault(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase));
        if(結果 is null) {
            return;
        }

        var 行 = this.ベンチマーク行を検索(結果);
        行?.実行結果を適用(結果,this.割当CPUを検索(結果),DateTimeOffset.Now,false);
    }

    private void フォールバック用完了結果項目を記憶() {
        var 完了 = this.結果項目一覧
            .Where(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)&&項目.平均ナノ秒 is not null)
            .ToArray();
        if(完了.Length>0) {
            this.直近完了結果項目一覧=完了;
        }
    }

    /// <summary>
    /// 進行更新バッチ深度 フィールドです。
    /// </summary>
    private int 進行更新バッチ深度;

    /// <summary>
    /// 進行更新保留中か フィールドです。
    /// </summary>
    private bool 進行更新保留中か;

    /// <summary>
    /// 最終実行中進行更新日時 フィールドです。
    /// </summary>
    private DateTimeOffset 最終実行中進行更新日時 = DateTimeOffset.MinValue;

    /// <summary>
    /// 実行中進行更新間隔 フィールドです。
    /// </summary>
    private static readonly TimeSpan 実行中進行更新間隔 = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// 必 要 に 応 じ て 副 作 用 と し て 内 部 状 態 や 表 示 へ 反 映 し ま す 。
    /// </summary>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    private IDisposable 進行更新バッチを開始() {
        this.進行更新バッチ深度++;
        return new 進行更新バッチスコープ(this);
    }

    /// <summary>
    /// 進行更新バッチを終了 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 進行更新バッチを終了() {
        this.進行更新バッチ深度--;
        if(this.進行更新バッチ深度>0) {
            return;
        }
        if(this.進行更新保留中か) {
            this.進行更新保留中か=false;
            this.ソースタブ進行を更新本体();
        }
    }

    /// <summary>
    /// IDisposable プロパティです。
    /// </summary>
    /// <value>IDisposable の値を取得/設定します。</value>
    private sealed class 進行更新バッチスコープ(実行ビューモデル 所有者):IDisposable {
        private          bool     破棄済みか;
        public void Dispose() {
            if(this.破棄済みか) {
                return;
            }
            this.破棄済みか=true;
            所有者.進行更新バッチを終了();
        }
    }

    /// <summary>
    /// ソースタブ進行要求トークン フィールドです。
    /// </summary>
    private int ソースタブ進行要求トークン;

    /// <summary>
    /// ソースタブ進行更新中か フィールドです。
    /// </summary>
    private bool ソースタブ進行更新中か;

    /// <summary>
    /// ソースタブ進行更新保留中か フィールドです。
    /// </summary>
    private bool ソースタブ進行更新保留中か;

    /// <summary>
    /// ソースタブ進行入力 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <param name="定義">定義 を指定して実行条件を制御します。</param>
    /// <param name="セル一覧">セル一覧 を指定して実行条件を制御します。</param>
    /// <param name="セッション結果一覧">セッション結果一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct ソースタブ進行入力(
        ソースタブ選択肢 選択肢,
        ベンチマークソースタブ定義 定義,
        IReadOnlyList<ベンチマークタブ進行セル> セル一覧,
        IReadOnlyList<ベンチマーク結果項目> セッション結果一覧,
        int 測定済み選択値セル一覧,
        int 選択値セル一覧);

    /// <summary>
    /// 進行測定一覧を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="セッション結果一覧">セッション結果一覧 を指定して実行条件を制御します。</param>
    /// <param name="結果項目一覧">結果項目一覧 を指定して実行条件を制御します。</param>
    /// <param name="状態ジョブ一覧">状態ジョブ一覧 を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<ベンチマーク要素測定> 進行測定一覧を構築(
        IReadOnlyList<ベンチマーク結果項目> セッション結果一覧,
        IReadOnlyList<ベンチマーク結果項目> 結果項目一覧,
        IReadOnlyList<ベンチマークジョブ>? 状態ジョブ一覧,
        ベンチマークソースタブ ソースタブ) {
        IEnumerable<ベンチマーク結果項目> ソースを列挙() {
            foreach(var 項目 in セッション結果一覧) {
                yield return 項目;
            }

            foreach(var 項目 in 結果項目一覧) {
                if(項目.ソースタブ==ソースタブ) {
                    yield return 項目;
                }
            }

            if(状態ジョブ一覧 is null) {
                yield break;
            }

            foreach(var ジョブ in 状態ジョブ一覧) {
                if(ジョブ.ソースタブ==ソースタブ) {
                    yield return new ベンチマーク結果項目(ジョブ);
                }
            }
        }

        return ソースを列挙()
            .Where(static 項目 => 項目.軸数>0)
            .GroupBy(static 項目 => 結果識別キーを作成(項目.プロジェクト,項目.型完全名,項目.メソッド,項目.軸数,項目.引数キー),StringComparer.OrdinalIgnoreCase)
            .Select(static グループ => グループ.Last())
            .Select(static 項目 => new ベンチマーク要素測定(
                項目.プロジェクト,
                項目.メソッド,
                項目.型完全名,
                項目.引数キー,
                項目.軸数,
                項目.状態,
                項目.平均ナノ秒,
                項目.合計ナノ秒))
            .ToArray();
    }

    /// <summary>
    /// 進行セル一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークタブ進行セル> 進行セル一覧を作成(ベンチマークソースタブ定義 ソースタブ) {
        var 実行計画を使用するか=this.ベンチマーク実行有効か||this.現在実行セッション.結果ありか;
        if(実行計画を使用するか) {
            return this.現在実行進行セル一覧を作成(ソースタブ);
        }

        var 状態 = this.ソースタブ表示状態を取得(ソースタブ.タブ);
        if(状態.行一覧.Count>0) {
            var 行スナップショット = 状態.行一覧.ToArray();
            return 行スナップショット
                .SelectMany(行 => 行.プロジェクトセル一覧.Select(セル => 進行セルを作成(行,セル.プロジェクト,行.選択中か&&セル.ケースありか)))
                .ToArray();
        }

        return ベンチマーク行グループ化ロジック.行を作成(this.カタログケース一覧,ソースタブ)
            .SelectMany(行 => ソースタブ.プロジェクト順.Select(プロジェクト => 進行セルを作成(
                行,
                プロジェクト,
                行.プロジェクト別ケース.ContainsKey(プロジェクト)
                    &&this.ベンチマーク行選択状態.TryGetValue(行選択キーを作成(ソースタブ,行.行キー),out var 選択中か)
                    &&選択中か)))
            .ToArray();
    }

    /// <summary>
    /// 現在実行進行セル一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークタブ進行セル> 現在実行進行セル一覧を作成(ベンチマークソースタブ定義 ソースタブ) {
        if(!this.ベンチマーク実行有効か&&!this.現在実行セッション.結果ありか) {
            return [];
        }

        var タブ別進行セル一覧=Volatile.Read(ref this.現在実行タブ別進行セル一覧);
        return タブ別進行セル一覧.TryGetValue(ソースタブ.タブ,out var セル一覧)
            ? セル一覧
            : [];
    }

    /// <summary>
    /// 実行要素の完了強調を維持 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="実行中要素数">実行中要素数 を指定して実行条件を制御します。</param>
    private void 実行要素の完了強調を維持(int 実行中要素数) {
        if(実行中要素数<=0) {
            return;
        }

        foreach(var 行 in this.全ベンチマーク行を取得()) {
            行.実行要素の完了強調を維持(実行中要素数);
        }
    }

    /// <summary>
    /// 最新終了要素数を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static int? 最新終了要素数を検索(進行状態? 状態,ベンチマークソースタブ定義 ソースタブ) {
        return 状態?.ジョブ一覧
            .Where(ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)>0&&終了ジョブか(ジョブ)&&ソースタブ.プロジェクトを含むか(ジョブ.プロジェクト))
            .OrderBy(static ジョブ => ジョブ.終了日時??ジョブ.開始日時??DateTimeOffset.MinValue)
            .ThenBy(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ))
            .LastOrDefault()
            is { } ジョブ ? ベンチマーク作業軸.軸数を取得(ジョブ) : null;
    }

    /// <summary>
    /// 最終完了要求トークン フィールドです。
    /// </summary>
    private int 最終完了要求トークン;

    /// <summary>
    /// 最終完了更新中か フィールドです。
    /// </summary>
    private bool 最終完了更新中か;

    /// <summary>
    /// 最終完了更新保留中か フィールドです。
    /// </summary>
    private bool 最終完了更新保留中か;

    /// <summary>
    /// 最終完了通知日時 フィールドです。
    /// </summary>
    private DateTimeOffset 最終完了通知日時 = DateTimeOffset.MinValue;

    /// <summary>
    /// 最終完了通知間隔 フィールドです。
    /// </summary>
    private static readonly TimeSpan 最終完了通知間隔 = TimeSpan.FromMilliseconds(500);

    // 完了判定は O(行数×measurements件数) で実行が進むほど重くなる(観測: 1tickで700〜1300ms)。
    // UIスレッドに残すのは「生の結果配列の確保(参照コピー)」と「SetFinalCompleted反映」のみで、
    // 選択フィルタ(結果が表示選択中か)・measurement射影・判定計算は全てバックグラウンド。
    // 行の 行キー/メソッド名/プロジェクト別ケース/プロジェクトセル一覧 はコンストラクタ後不変、行.選択中か は
    // boolのatomic読みなので、バックグラウンドから読んでも安全(Phase Bの比較行構築と同じ前提)。
    private readonly record struct 最終完了タブ入力(
        ソースタブ選択肢 選択肢,
        ベンチマークソースタブ表示状態 状態,
        IReadOnlyList<ベンチマーク結果項目> ソース,
        ベンチマーク表行[] 行一覧,
        IReadOnlyList<(string 左プロジェクト,string 右プロジェクト)> ペア一覧,
        int 最大要素数);

    private readonly record struct 最終完了タブ結果(
        ソースタブ選択肢 選択肢,
        ベンチマーク表行[] 行一覧,
        bool[]? 完了,
        bool 最終完了か);

    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void 最終完了状態を更新() {
        if(this.最終完了更新中か) {
            this.最終完了更新保留中か=true;
            return;
        }

        var 現在日時 = DateTimeOffset.UtcNow;
        if(this.ベンチマーク実行有効か&&現在日時-this.最終完了通知日時<最終完了通知間隔) {
            // 実行中は後続の進捗更新でも再度呼ばれるため、スキップしても収束する。
            return;
        }

        this.最終完了通知日時=現在日時;
        var 結果項目一覧スナップショット = this.結果項目一覧.ToArray();
        var タブ入力一覧 = this.ソースタブ一覧
            .Select(選択肢 => {
                var 状態 = this.ソースタブ表示状態を取得(選択肢.定義.タブ);
                var ソース = this.表示に今回実行メモリを使うか(状態.定義)
                    ? this.現在実行結果項目一覧を取得(状態.定義.タブ).ToArray()
                    : 結果項目一覧スナップショット;
                return new 最終完了タブ入力(
                選択肢,
                状態,
                ソース,
                状態.行一覧.ToArray(),
                ベンチマークペア一覧を作成(状態.定義),
                this.最終完了最大要素数を取得(状態.定義,ソース));
            })
            .ToArray();
        var 閾値ナノ秒 = this.適応閾値を取得().TotalMilliseconds*1_000_000;
        var トークン = ++this.最終完了要求トークン;
        var 世代 = this.実行結果世代;
        this.最終完了更新中か=true;
        var ストップウォッチ = Stopwatch.StartNew();
        Task.Run(() => タブ入力一覧
            .Select(入力 => {
                var 測定一覧 = this.表示測定一覧を作成(入力.ソース,入力.状態.定義);
                if(測定一覧.Length==0) {
                    return new 最終完了タブ結果(入力.選択肢,入力.行一覧,null,false);
                }

                var 完了 = 最終完了状態を計算(測定一覧,入力.行一覧,入力.ペア一覧,入力.最大要素数,閾値ナノ秒);
                return new 最終完了タブ結果(
                    入力.選択肢,
                    入力.行一覧,
                    完了,
                    タブ最終完了か(測定一覧,入力.行一覧,入力.最大要素数,閾値ナノ秒));
            })
            .ToArray())
            .ContinueWith(タスク => {
                UIスレッド予算.記録("bg:completion",ストップウォッチ.Elapsed.TotalMilliseconds);
                this.UIディスパッチャー.呼び出し開始(() => {
                    this.最終完了更新中か=false;
                    if(トークン==this.最終完了要求トークン&&世代==this.実行結果世代&&!タスク.IsFaulted) {
                        var 完了タブ一覧 = タスク.Result;
                        UIスレッド予算.計測("completion-apply",() => {
                            foreach(var 完了タブ in 完了タブ一覧) {
                                完了タブ.選択肢.最終完了状態を設定(完了タブ.最終完了か);

                                if(完了タブ.完了 is null) {
                                    foreach(var 行 in 完了タブ.行一覧) {
                                        行.最終完了状態を設定(false);
                                    }
                                } else {
                                    for(var インデックス = 0;インデックス<完了タブ.行一覧.Length;インデックス++) {
                                        完了タブ.行一覧[インデックス].最終完了状態を設定(完了タブ.完了[インデックス]);
                                    }
                                }
                            }
                        });
                    } else if(タスク.IsFaulted) {
                        起動診断.書き込む("バックグラウンドでの最終完了状態更新に失敗しました。",タスク.Exception);
                    }

                    if(this.最終完了更新保留中か) {
                        this.最終完了更新保留中か=false;
                        this.最終完了状態を更新();
                    }
                });
            },TaskScheduler.Default);
    }

    private static ベンチマークタブ進行 測定セル進行フォールバックを適用(
        ベンチマークタブ進行 進行,
        int 測定済み選択値セル一覧,
        int 選択値セル一覧) {
        if(選択値セル一覧==0||測定済み選択値セル一覧<=進行.完了数) {
            return 進行;
        }

        var 合計 = 進行.合計数>0 ? 進行.合計数 : 選択値セル一覧;
        return new ベンチマークタブ進行(Math.Min(測定済み選択値セル一覧,合計),合計);
    }

    private void 最終完了状態を同期適用() {
        this.最終完了更新保留中か=false;
        _=unchecked(++this.最終完了要求トークン);
        var 結果項目一覧スナップショット = this.結果項目一覧.ToArray();
        var 閾値ナノ秒 = this.適応閾値を取得().TotalMilliseconds*1_000_000;
        var 完了タブ一覧 = this.ソースタブ一覧
            .Select(選択肢 => {
                var 状態 = this.ソースタブ表示状態を取得(選択肢.定義.タブ);
                var ソース = this.表示に今回実行メモリを使うか(状態.定義)
                    ? this.現在実行結果項目一覧を取得(状態.定義.タブ).ToArray()
                    : 結果項目一覧スナップショット;
                var 行一覧 = 状態.行一覧.ToArray();
                var 測定一覧 = this.表示測定一覧を作成(ソース,状態.定義);
                if(測定一覧.Length==0) {
                    return new 最終完了タブ結果(
                        選択肢,
                        行一覧,
                        new bool[行一覧.Length],
                        false);
                }

                var ペア一覧 = ベンチマークペア一覧を作成(状態.定義);
                var 最大要素数 = this.最終完了最大要素数を取得(状態.定義,ソース);
                var 完了 = 最終完了状態を計算(測定一覧,行一覧,ペア一覧,最大要素数,閾値ナノ秒);
                return new 最終完了タブ結果(
                    選択肢,
                    行一覧,
                    完了,
                    タブ最終完了か(測定一覧,行一覧,最大要素数,閾値ナノ秒));
            })
            .ToArray();
        foreach(var 完了タブ in 完了タブ一覧) {
            完了タブ.選択肢.最終完了状態を設定(完了タブ.最終完了か);

            if(完了タブ.完了 is null) {
                foreach(var 行 in 完了タブ.行一覧) {
                    行.最終完了状態を設定(false);
                }
            } else {
                for(var インデックス = 0;インデックス<完了タブ.行一覧.Length;インデックス++) {
                    完了タブ.行一覧[インデックス].最終完了状態を設定(完了タブ.完了[インデックス]);
                }
            }
        }
    }

    /// <summary>
    /// 最終完了状態を計算 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="測定一覧">測定一覧 を指定して実行条件を制御します。</param>
    /// <param name="行一覧">行一覧 を指定して実行条件を制御します。</param>
    /// <param name="ペア一覧">ペア一覧 を指定して実行条件を制御します。</param>
    /// <param name="最大要素数">最大要素数 を指定して実行条件を制御します。</param>
    /// <param name="閾値ナノ秒">閾値ナノ秒 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool[] 最終完了状態を計算(
        ベンチマーク要素測定[] 測定一覧,
        ベンチマーク表行[] 行一覧,
        IReadOnlyList<(string 左プロジェクト,string 右プロジェクト)> ペア一覧,
        int 最大要素数,
        double 閾値ナノ秒) {
        var 測定一覧別メソッド = 測定一覧.ToLookup(static 測定 => 測定.メソッド,StringComparer.OrdinalIgnoreCase);
        var 結果一覧 = new bool[行一覧.Length];
        for(var インデックス = 0;インデックス<行一覧.Length;インデックス++) {
            var 行 = 行一覧[インデックス];
            var 行測定一覧 = 測定一覧別メソッド[行.メソッド名].ToArray();
            var 対象ペア一覧=ペア一覧.Where(ペア=>
                行.プロジェクトセルを取得(ペア.左プロジェクト)?.ケースありか==true
                &&行.プロジェクトセルを取得(ペア.右プロジェクト)?.ケースありか==true).ToArray();
            結果一覧[インデックス]=対象ペア一覧.Length>0&&対象ペア一覧.All(ペア => {
                var 左セル = 進行セルを作成(行,ペア.左プロジェクト,true);
                var 右セル = 進行セルを作成(行,ペア.右プロジェクト,true);
                return ベンチマーク作業軸.ループ回数を使うか(行.ソースタブ.タブ)
                    ? ベンチマーク行完了ロジック.ループ回数最終完了か(行測定一覧,左セル,右セル,閾値ナノ秒)
                    : ベンチマーク行完了ロジック.最終完了か(行測定一覧,左セル,右セル,最大要素数,閾値ナノ秒);
            })&&理想列が完了済みか(行,行測定一覧,閾値ナノ秒);
        }

        return 結果一覧;
    }

    // 理想列は較正役(右)と同じループ回数で測り終えたときに完了とする。理想版の無い行(保存層の行など)は条件に含めない。
    private static bool 理想列が完了済みか(ベンチマーク表行 行,ベンチマーク要素測定[] 行測定一覧,double 閾値ナノ秒) {
        if(!行.ソースタブ.理想ありか
            ||行.プロジェクトセルを取得(行.ソースタブ.理想プロジェクト)?.ケースありか!=true
            ||!ベンチマーク作業軸.ループ回数を使うか(行.ソースタブ.タブ)) {
            return true;
        }
        return ベンチマーク行完了ロジック.ループ回数最終完了か(
            行測定一覧,
            進行セルを作成(行,行.ソースタブ.左プロジェクト,true),
            進行セルを作成(行,行.ソースタブ.理想プロジェクト,true),
            閾値ナノ秒);
    }

    /// <summary>
    /// 最終完了状態をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 最終完了状態をクリア() {
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            行.最終完了状態を設定(false);
        }
    }

    /// <summary>
    /// 最終完了最大要素数を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private int 最終完了最大要素数を取得() {
        return this.最終完了最大要素数を取得(this.選択ソース定義);
    }

    private int 最終完了最大要素数を取得(ベンチマークソースタブ定義 ソースタブ,IReadOnlyList<ベンチマーク結果項目>? ソース = null) {
        var ソース最大 = ソース?
            .Where(結果 => ソースタブ.プロジェクトを含むか(結果.プロジェクト))
            .Select(static 結果 => 結果.軸数)
            .Where(static 項目 => 項目>0)
            .DefaultIfEmpty(0)
            .Max()??0;
        if(ソース最大>0) {
            return ソース最大;
        }

        if(this.ベンチマーク実行有効か||this.現在実行セッション.ソースタブ結果があるか(ソースタブ.タブ)) {
            return this.選択要素数一覧を取得().LastOrDefault();
        }

        var 実行要素件数一覧 = this.選択実行選択肢?.要素数一覧
            .Where(static 項目 => 項目>0)
            .Order()
            .ToArray();
        return 実行要素件数一覧 is { Length:>0 }
            ? 実行要素件数一覧[^1]
            : this.選択要素数一覧を取得().LastOrDefault();
    }

    /// <summary>
    /// 直前の完了ペア要素数を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="型完全名">型完全名 を指定して実行条件を制御します。</param>
    /// <param name="左プロジェクト">左プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="右プロジェクト">右プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="選択値要素数">選択値要素数 を指定して実行条件を制御します。</param>
    /// <param name="引数キー">引数キー を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private int? 直前の完了ペア要素数を検索(
        string メソッド,
        string 型完全名,
        string 左プロジェクト,
        string 右プロジェクト,
        int 選択値要素数,
        string 引数キー = "") {
        var 測定一覧 = this.表示用結果ソースを取得()
            .Select(static 結果 => new ベンチマーク要素測定(
                結果.プロジェクト,
                結果.メソッド,
                結果.型完全名,
                結果.引数キー,
                結果.軸数,
                結果.状態,
                結果.平均ナノ秒,
                結果.合計ナノ秒));
        return ベンチマークスキップ表示ロジック.直前の完了ペア要素数を検索(
            測定一覧,
            メソッド,
            型完全名,
            引数キー,
            左プロジェクト,
            右プロジェクト,
            選択値要素数);
    }
}
