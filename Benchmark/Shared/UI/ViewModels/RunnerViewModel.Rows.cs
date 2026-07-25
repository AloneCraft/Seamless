using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Seamless.UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// ベンチマーク行の構築・索引・検索
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// カタログサービス フィールドです。
    /// </summary>
    private readonly Iベンチマークカタログサービス カタログサービス;

    /// <summary>
    /// ベンチマーク行選択状態 フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> ベンチマーク行選択状態 = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// プロジェクトメソッド別ベンチマークケース フィールドです。
    /// </summary>
    private readonly Dictionary<string,ベンチマークケース> プロジェクトメソッド別ベンチマークケース = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// 結果キー別ベンチマーク行 フィールドです。
    /// 値が null のキーは、複数行が同じキーに一致した(曖昧)ため意図的に無効化されたことを表す。
    /// </summary>
    private readonly Dictionary<string,ベンチマーク表行?> 結果キー別ベンチマーク行 = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// プロジェクトメソッド別ベンチマーク行 フィールドです。
    /// 値が null のキーは、複数行が同じキーに一致した(曖昧)ため意図的に無効化されたことを表す。
    /// </summary>
    private readonly Dictionary<string,ベンチマーク表行?> プロジェクトメソッド別ベンチマーク行 = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// カタログケース一覧 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークケース> カタログケース一覧 = [];

    /// <summary>
    /// 現在実行選択ベンチマークケース一覧 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークケース> 現在実行選択ベンチマークケース一覧 = [];

    /// <summary>
    /// ベンチマーク行一覧 プロパティです。
    /// </summary>
    /// <value>ベンチマーク行一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク表行> ベンチマーク行一覧 => this.選択ソースタブ表示状態.行一覧;

    /// <summary>
    /// 新規グリッド作成可能か プロパティです。
    /// </summary>
    /// <value>新規グリッド作成可能か の値を取得/設定します。</value>
    public bool 新規グリッド作成可能か =>
        !this.更新中か
        &&!this.実行履歴読込中か
        &&!this.選択実行DB読込中か
        &&!this.ベンチマーク実行有効か
        &&!this.ベンチマーク一時停止中か;

    /// <summary>
    /// 全ベンチマーク行を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<ベンチマーク表行> 全ベンチマーク行を取得() =>
        this.ソースタブ表示状態一覧値.SelectMany(static 状態 => 状態.行一覧);

    /// <summary>
    /// ベンチマーク行を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    public void ベンチマーク行を切り替え(ベンチマーク表行 行) {
        this.ベンチマーク行一覧を切り替え([行]);
    }

    internal void ベンチマーク行一覧を切り替え(IEnumerable<ベンチマーク表行> 行一覧) {
        if(!this.ベンチマーク選択を変更できるか) {
            return;
        }

        var 対象一覧 = 行一覧
            .Where(static 項目 => 項目.プロジェクトセル一覧.Any(static セル => セル.ケースありか))
            .Distinct()
            .ToArray();
        var 選択値 = グリッドチェック切替ロジック.目標チェック状態を解決(
            対象一覧.Select(static 行 => 行.選択チェック状態));
        if(選択値 is not { } 値か) {
            return;
        }

        this.ベンチマーク行一覧を設定(対象一覧,値か);
    }

    internal void ベンチマーク行UI選択を同期(
        ベンチマークソースタブ表示状態 状態,
        IReadOnlyList<ベンチマーク表行> 追加行一覧,
        IReadOnlyList<ベンチマーク表行> 削除済み行一覧) {
        foreach(var 削除済み in 削除済み行一覧) {
            行UI選択を設定(削除済み,false);
        }

        foreach(var 追加 in 追加行一覧) {
            行UI選択を設定(追加,true);
        }
    }

    /// <summary>
    /// カタログからベンチマーク行を再構築 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void カタログからベンチマーク行を再構築() {
        using(this.データグリッド更新バッチを開始()) {
            var 要素数 = this.選択結果要素数?.値??0;
            var 構築済み行一覧 = this.ソースタブ表示状態一覧値
                .AsParallel()
                .AsOrdered()
                .WithDegreeOfParallelism(Math.Max(1,Math.Min(Environment.ProcessorCount,this.ソースタブ表示状態一覧値.Count)))
                .Select(状態 => (状態: 状態,行一覧: this.メソッド行一覧を作成(this.カタログケース一覧,状態.定義,要素数)))
                .ToArray();
            foreach(var 項目 in 構築済み行一覧) {
                var 行一覧 = 項目.行一覧;
                foreach(var 行 in 行一覧) {
                    this.ベンチマーク行を購読(行);
                    this.保存ベンチマーク選択を適用(行);
                }
                項目.状態.行一覧.範囲を置換(行一覧);
            }
            this.ベンチマーク行インデックスを再構築();
            this.ベンチマークソート列を更新();
        }
    }

    /// <summary>
    /// メソッド行一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ケース一覧">ケース一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク表行> メソッド行一覧を作成(IReadOnlyList<ベンチマークケース> ケース一覧) =>
        this.メソッド行一覧を作成(ケース一覧,this.選択ソース定義);

    /// <summary>
    /// メソッド行一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ケース一覧">ケース一覧 を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク表行> メソッド行一覧を作成(IReadOnlyList<ベンチマークケース> ケース一覧,ベンチマークソースタブ定義 ソースタブ) {
        return this.メソッド行一覧を作成(ケース一覧,ソースタブ,this.選択結果要素数?.値??0);
    }

    /// <summary>
    /// メソッド行一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ケース一覧">ケース一覧 を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク表行> メソッド行一覧を作成(IReadOnlyList<ベンチマークケース> ケース一覧,ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        return ベンチマーク行グループ化ロジック.行を作成(ケース一覧,ソースタブ,要素数)
            .Select(行 => new ベンチマーク表行(
                行.行キー,
                行.メソッド名,
                行.ベンチマーク名,
                ソースタブ,
                行.プロジェクト別ケース))
            .ToArray();
    }

    /// <summary>
    /// ベンチマークケースインデックスを更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="ケース一覧">ケース一覧 を指定して実行条件を制御します。</param>
    private void ベンチマークケースインデックスを更新(IReadOnlyList<ベンチマークケース> ケース一覧) {
        this.プロジェクトメソッド別ベンチマークケース.Clear();
        foreach(var ベンチマーク in ケース一覧) {
            this.プロジェクトメソッド別ベンチマークケース[ベンチマークケースキーを作成(ベンチマーク.メソッド名,ベンチマーク.プロジェクト,ベンチマーク.型完全名,ベンチマーク.引数キー)]=ベンチマーク;
        }
    }

    /// <summary>
    /// ベンチマーク行インデックスを再構築 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマーク行インデックスを再構築() {
        this.結果キー別ベンチマーク行.Clear();
        this.プロジェクトメソッド別ベンチマーク行.Clear();
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            foreach(var 項目 in 行.プロジェクト別ケース) {
                var ベンチマーク = 項目.Value;
                this.ベンチマーク行インデックスを追加(行,ベンチマーク.プロジェクト,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー);
                if(!スケーラブルソースタブか(行.ソースタブ.タブ)) {
                    this.ベンチマーク行インデックスを追加(行,ベンチマーク.プロジェクト,string.Empty,ベンチマーク.メソッド名,string.Empty);
                }
                this.曖昧さを考慮して登録(this.プロジェクトメソッド別ベンチマーク行,ベンチマーク行メソッドプロジェクトインデックスキーを作成(行.ソースタブ.タブ,ベンチマーク.プロジェクト,ベンチマーク.メソッド名),行);
            }
        }
    }

    /// <summary>
    /// ベンチマーク行インデックスを追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="型完全名">型完全名 を指定して実行条件を制御します。</param>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="引数キー">引数キー を指定して実行条件を制御します。</param>
    private void ベンチマーク行インデックスを追加(ベンチマーク表行 行,string プロジェクト,string 型完全名,string メソッド,string 引数キー) {
        this.曖昧さを考慮して登録(this.結果キー別ベンチマーク行,ベンチマーク行結果インデックスキーを作成(行.ソースタブ.タブ,プロジェクト,型完全名,メソッド,引数キー),行);
    }

    private void ベンチマーク行一覧を設定(IEnumerable<ベンチマーク表行> 行一覧,bool 選択中か) {
        if(!this.ベンチマーク選択を変更できるか) {
            return;
        }

        var 対象一覧 = 行一覧.Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか)).Distinct().ToArray();
        if(対象一覧.Length==0) {
            this.ベンチマーク選択集計を更新();
            return;
        }

        this.ベンチマーク選択適用中か=true;
        try {
            foreach(var 行 in 対象一覧) {
                行.選択中か=選択中か;
            }
            this.ベンチマーク選択集計を更新();
        } finally {
            this.ベンチマーク選択適用中か=false;
        }
        this.UI状態を保存();
        this.ベンチマーク選択表示を更新();
    }

    /// <summary>
    /// ベンチマーク行を購読 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    private void ベンチマーク行を購読(ベンチマーク表行 行) {
        行.PropertyChanged+=(_,引数) => {
            if(引数.PropertyName==nameof(ベンチマーク表行.選択中か)&&!this.UI状態適用中か&&!this.ベンチマーク選択適用中か) {
                this.ベンチマーク選択集計を更新();
                this.UI状態を保存();
                this.ベンチマーク選択表示を更新();
            }
        };
    }

    /// <summary>
    /// ベンチマーク行一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<ベンチマーク表行> ベンチマーク行一覧を取得(ベンチマークソースタブ定義 ソースタブ) =>
        this.ソースタブ表示状態を取得(ソースタブ.タブ).行一覧;

    private static bool? 行選択状態を取得(IEnumerable<ベンチマーク表行> 行一覧) {
        var 対象一覧 = 行一覧.Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか)).ToArray();
        if(対象一覧.Length==0) {
            return false;
        }

        var 選択値 = 対象一覧.Count(static 行 => 行.選択中か);
        return 選択値==0
            ? false
            : 選択値==対象一覧.Length
                ? true
                : null;
    }

    private IReadOnlyList<string> ベンチマーク選択行キー一覧を取得(ベンチマーク表行 行) {
        var キー一覧 = new List<string> { 行.行キー };
        var グループ化行キー = ベンチマーク行グループ化ロジック.行キーを作成(行.ソースタブ,行.メソッド名,行.プロジェクト別ケース);
        if(!キー一覧.Contains(グループ化行キー,StringComparer.OrdinalIgnoreCase)) {
            キー一覧.Add(グループ化行キー);
        }
        var 表示キー = スナップショット行キーを作成(
            行.ベンチマーク型文字列,
            行.メソッド名,
            行.プロジェクト別ケース.Values.FirstOrDefault()?.引数キー??string.Empty);
        if(!キー一覧.Contains(表示キー,StringComparer.OrdinalIgnoreCase)) {
            キー一覧.Add(表示キー);
        }
        return キー一覧;
    }

    /// <summary>
    /// 現在実行選択ベンチマークケース一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークケース> 現在実行選択ベンチマークケース一覧を取得() =>
        Volatile.Read(ref this.現在実行選択ベンチマークケース一覧);

    /// <summary>
    /// 今回の実行で計測予定のケースに対応するセルへ「実行待ち(時計)」表示を適用します。
    /// 前回実行の値が維持表示されていても、計測が届くまで完了系アイコンではなく待機を示します。
    /// 再開実行では選択値が既に成功済みケースを除外しているため、完了済みセルには適用されません。
    /// </summary>
    private void 今回実行待機表示を適用(IReadOnlyList<ベンチマークケース> 選択値) {
        if(選択値.Count==0) {
            return;
        }

        var 対象キー一覧 = 選択値
            .Select(static ケース => string.Join("|",ケース.プロジェクト,ケース.型完全名,ケース.メソッド名,ケース.引数キー))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            foreach(var 組 in 行.プロジェクト別ケース) {
                var ケース = 組.Value;
                if(対象キー一覧.Contains(string.Join("|",ケース.プロジェクト,ケース.型完全名,ケース.メソッド名,ケース.引数キー))) {
                    行.プロジェクトセルを取得(組.Key)?.今回実行待機を設定(true);
                }
            }
        }
    }

    private IEnumerable<ベンチマークケース> 成功結果なしベンチマークケースを抽出(
        IReadOnlyList<ベンチマークケース> 選択値,
        IReadOnlyList<int> 要素件数一覧) {
        foreach(var ベンチマーク in 選択値) {
            if(!this.全選択軸に成功結果があるか(ベンチマーク,要素件数一覧)) {
                yield return ベンチマーク;
            }
        }
    }

    private bool 全選択軸に成功結果があるか(ベンチマークケース ベンチマーク,IReadOnlyList<int> 要素件数一覧) {
        if(要素件数一覧.Count==0) {
            return false;
        }

        var 結果一覧 = this.全グリッド結果項目を取得();
        foreach(var 要素数 in 要素件数一覧) {
            var 成功ありか = 結果一覧.Any(結果 =>
                string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)
                &&結果.平均ナノ秒 is not null
                &&結果が一致するか(結果,ベンチマーク.メソッド名,ベンチマーク.プロジェクト,ベンチマーク.型完全名,要素数,ベンチマーク.引数キー));
            if(!成功ありか) {
                return false;
            }
        }

        return true;
    }

    // 選択スナップショットとキュー生成の規則は UI.Core の ベンチマーク選択キューロジック に集約する。
    // 表示中タブだけの生きた状態(チェックボックス)を優先する。
    /// <summary>
    /// 全タブから選択ベンチマークケースを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークケース> 全タブから選択ベンチマークケースを作成() {
        this.現在ベンチマーク選択を取得();
        return this.ソースタブ一覧
            .SelectMany(選択肢 => this.選択ベンチマークケースを作成(選択肢.定義))
            .GroupBy(
                static ベンチマーク => string.Join("|",ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー),
                StringComparer.OrdinalIgnoreCase)
            .Select(static グループ => グループ.First())
            .ToArray();
    }

    private IEnumerable<ベンチマークケース> 選択ベンチマークケースを作成(ベンチマークソースタブ定義 ソースタブ) {
        var 状態 = this.ソースタブ表示状態を取得(ソースタブ.タブ);
        if(状態.行一覧.Count>0) {
            return 状態.行一覧
                .SelectMany((行,インデックス) => 行.選択中か
                    ? 行.実行ケースを取得().Select(ベンチマーク => new ベンチマークケース(
                        ベンチマーク.プロジェクト,
                        ベンチマーク.アセンブリ名,
                        ベンチマーク.型完全名,
                        ベンチマーク.メソッド名,
                        ベンチマーク.引数キー,
                        ベンチマーク.引数一覧) {
                        バインド行インデックス=インデックス,
                    })
                    : []);
        }

        return ベンチマーク行グループ化ロジック.行を作成(this.カタログケース一覧,ソースタブ)
            .Where(行 => 行.プロジェクト別ケース.Count>0
                &&this.ベンチマーク行選択状態.TryGetValue(行選択キーを作成(ソースタブ,行.行キー),out var 選択中か)
                &&選択中か)
            .SelectMany(行 => ソースタブ.プロジェクト順
                .Where(プロジェクト => 行.プロジェクト別ケース.ContainsKey(プロジェクト))
                .Select(プロジェクト => 行.プロジェクト別ケース[プロジェクト]));
    }

    /// <summary>
    /// 行選択キーを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="行キー">行キー を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string 行選択キーを作成(ベンチマークソースタブ定義 ソースタブ,string 行キー) =>
        ベンチマーク選択キューロジック.行選択キーを作成(ソースタブ,行キー);

    /// <summary>
    /// 型完全名 プロパティです。
    /// </summary>
    /// <value>型完全名 の値を取得/設定します。</value>
    private ベンチマークケース? ベンチマークケースを検索(string メソッド,string プロジェクト,string 型完全名 = "",string 引数キー = "",ベンチマークソースタブ? ソースタブ = null) {
        if(this.プロジェクトメソッド別ベンチマークケース.TryGetValue(ベンチマークケースキーを作成(メソッド,プロジェクト,型完全名,引数キー),out var ベンチマーク)) {
            return ベンチマーク;
        }

        if(ソースタブ is { } タブ&&タブ!=this.選択ソース定義.タブ) {
            return null;
        }

        return this.ベンチマーク行一覧
            .FirstOrDefault(行 => 行.プロジェクト別ケース.TryGetValue(プロジェクト,out var 候補)
                &&string.Equals(候補.メソッド名,メソッド,StringComparison.OrdinalIgnoreCase)
                &&(string.IsNullOrWhiteSpace(型完全名)
                    ||string.Equals(候補.型完全名,型完全名,StringComparison.OrdinalIgnoreCase))
                &&(string.IsNullOrWhiteSpace(引数キー)
                    ||string.Equals(候補.引数キー,引数キー,StringComparison.OrdinalIgnoreCase)))
            ?.プロジェクト別ケース
            .GetValueOrDefault(プロジェクト);
    }

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private static string ベンチマークケースキーを作成(string メソッド,string プロジェクト,string 型完全名,string 引数キー = "") =>
        ベンチマーク行グループ化ロジック.ケースキーを作成(プロジェクト,型完全名,メソッド,引数キー);

    /// <summary>
    /// ベンチマーク行を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク表行? ベンチマーク行を検索(ベンチマーク結果項目 結果) {
        var 状態 = this.ソースタブ表示状態を取得(結果.ソースタブ);
        if(結果.バインド行インデックス>=0
            &&結果.バインド行インデックス<状態.行一覧.Count
            &&行の結果か(状態.行一覧[結果.バインド行インデックス],結果)) {
            return 状態.行一覧[結果.バインド行インデックス];
        }

        return this.キーからベンチマーク行を解決(結果.ソースタブ,結果.プロジェクト,結果.型完全名,結果.メソッド,結果.引数キー)
            ??一意な候補行(this.ベンチマーク行一覧,候補 => 行の結果か(候補,結果))
            ??(スケーラブルソースタブか(結果.ソースタブ)||結果.ソースタブ!=this.選択ソース定義.タブ ? null : 一意な候補行(this.ベンチマーク行一覧,候補 => string.Equals(候補.メソッド名,結果.メソッド,StringComparison.OrdinalIgnoreCase)
                &&候補.プロジェクトセルを取得(結果.プロジェクト) is { ケースありか: true }))
            ??(スケーラブルソースタブか(this.選択ソース定義.タブ)||!this.選択ソース定義.プロジェクトを含むか(結果.プロジェクト) ? null : 一意な候補行(this.ベンチマーク行一覧,候補 => string.Equals(候補.メソッド名,結果.メソッド,StringComparison.OrdinalIgnoreCase)
                &&候補.プロジェクトセルを取得(結果.プロジェクト) is { ケースありか: true }));
    }

    /// <summary>
    /// ベンチマーク行を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ジョブ">ジョブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク表行? ベンチマーク行を検索(ベンチマークジョブ ジョブ) {
        var 状態 = this.ソースタブ表示状態を取得(ジョブ.ソースタブ);
        if(ジョブ.バインド行インデックス>=0
            &&ジョブ.バインド行インデックス<状態.行一覧.Count
            &&行のジョブか(状態.行一覧[ジョブ.バインド行インデックス],ジョブ)) {
            return 状態.行一覧[ジョブ.バインド行インデックス];
        }

        return this.キーからベンチマーク行を解決(ジョブ.ソースタブ,ジョブ.プロジェクト,ジョブ.型完全名,ジョブ.メソッド名,ジョブ.引数キー)
            ??一意な候補行(this.ベンチマーク行一覧,候補 => 行のジョブか(候補,ジョブ))
            ??(スケーラブルソースタブか(ジョブ.ソースタブ)||ジョブ.ソースタブ!=this.選択ソース定義.タブ ? null : 一意な候補行(this.ベンチマーク行一覧,候補 => string.Equals(候補.メソッド名,ジョブ.メソッド名,StringComparison.OrdinalIgnoreCase)
                &&候補.プロジェクトセルを取得(ジョブ.プロジェクト) is { ケースありか: true }))
            ??(スケーラブルソースタブか(this.選択ソース定義.タブ)||!this.選択ソース定義.プロジェクトを含むか(ジョブ.プロジェクト) ? null : 一意な候補行(this.ベンチマーク行一覧,候補 => string.Equals(候補.メソッド名,ジョブ.メソッド名,StringComparison.OrdinalIgnoreCase)
                &&候補.プロジェクトセルを取得(ジョブ.プロジェクト) is { ケースありか: true }));
    }

    /// <summary>
    /// キーからベンチマーク行を解決 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="型完全名">型完全名 を指定して実行条件を制御します。</param>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="引数キー">引数キー を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク表行? キーからベンチマーク行を解決(ベンチマークソースタブ ソースタブ,string プロジェクト,string 型完全名,string メソッド,string 引数キー) =>
        this.ベンチマーク行検索を試みる(ソースタブ,プロジェクト,型完全名,メソッド,引数キー,out var 行) ? 行 : null;

    /// <summary>
    /// ベンチマーク行検索を試みる メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="型完全名">型完全名 を指定して実行条件を制御します。</param>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="引数キー">引数キー を指定して実行条件を制御します。</param>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool ベンチマーク行検索を試みる(ベンチマークソースタブ ソースタブ,string プロジェクト,string 型完全名,string メソッド,string 引数キー,out ベンチマーク表行 行) {
        if(this.結果キー別ベンチマーク行.TryGetValue(ベンチマーク行結果インデックスキーを作成(ソースタブ,プロジェクト,型完全名,メソッド,引数キー),out var 完全一致行)&&完全一致行 is not null) {
            行=完全一致行;
            return true;
        }

        if(!スケーラブルソースタブか(ソースタブ)
            &&this.結果キー別ベンチマーク行.TryGetValue(ベンチマーク行結果インデックスキーを作成(ソースタブ,プロジェクト,string.Empty,メソッド,string.Empty),out var ワイルドカード行)&&ワイルドカード行 is not null) {
            行=ワイルドカード行;
            return true;
        }

        if(this.プロジェクトメソッド別ベンチマーク行.TryGetValue(ベンチマーク行メソッドプロジェクトインデックスキーを作成(ソースタブ,プロジェクト,メソッド),out var プロジェクトメソッド行)&&プロジェクトメソッド行 is not null) {
            行=プロジェクトメソッド行;
            return true;
        }

        行=null!;
        return false;
    }

    /// <summary>
    /// ベンチマーク行結果インデックスキーを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="型完全名">型完全名 を指定して実行条件を制御します。</param>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="引数キー">引数キー を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string ベンチマーク行結果インデックスキーを作成(ベンチマークソースタブ ソースタブ,string プロジェクト,string 型完全名,string メソッド,string 引数キー) =>
        string.Join("|",ソースタブ,プロジェクト,型完全名,メソッド,引数キー);

    /// <summary>
    /// ベンチマーク行メソッドプロジェクトインデックスキーを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string ベンチマーク行メソッドプロジェクトインデックスキーを作成(ベンチマークソースタブ ソースタブ,string プロジェクト,string メソッド) =>
        string.Join("|",ソースタブ,プロジェクト,メソッド);

    private IReadOnlyList<表示ベンチマーク行選択> 表示ベンチマーク行選択を取得() {
        var 選択一覧 = new List<表示ベンチマーク行選択>();
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            if(行.選択中か) {
                選択一覧.Add(new(
                    行.ソースタブ.ID,
                    行.ベンチマーク型文字列,
                    行.メソッド名));
            }
        }

        return 選択一覧;
    }

    private void 表示ベンチマーク行選択を適用(IReadOnlyList<表示ベンチマーク行選択> 選択一覧) {
        var 選択値 = 選択一覧.ToHashSet();
        this.ベンチマーク選択適用中か=true;
        try {
            foreach(var 行 in this.全ベンチマーク行を取得()) {
                行.選択中か=選択値.Contains(new(
                    行.ソースタブ.ID,
                    行.ベンチマーク型文字列,
                    行.メソッド名));
            }
        } finally {
            this.ベンチマーク選択適用中か=false;
        }
        this.ベンチマーク選択集計を更新();
        this.現在ベンチマーク選択を取得();
    }

    private readonly record struct 表示ベンチマーク行選択(
        string ソースタブID,
        string ベンチマーク型文字列,
        string メソッド名);

    /// <summary>
    /// UI実行を作成Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行ソースタブ">実行ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="実行プロファイル">実行プロファイル を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private async Task<long?> UI実行を作成Async(ベンチマークソースタブ 実行ソースタブ,ベンチマーク実行プロファイル 実行プロファイル) {
        var 実行プロファイル文字列 = 実行プロファイル.ToString();
        var 表示名 = ベンチマークアプリプロファイル.タイトル+" "+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
        return await this.UI実行リポジトリ.実行を作成Async(UI実行アプリ種別を取得(),表示名,実行プロファイル文字列,実行ソースタブ);
    }

    /// <summary>
    /// 新規グリッドを作成 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 新規グリッドを作成() {
        if(!this.新規グリッド作成可能か) {
            return;
        }

        // 読込ベンチマークスナップショット項目を適用 と同様に、進行中/保留中の非同期比較行再計算が
        // このクリアより後にUIスレッドで完了し、古い比較行データでセルを上書きしないよう無効化する。
        this.実行結果世代=unchecked(this.実行結果世代+1);
        this.比較行要求トークン=unchecked(this.比較行要求トークン+1);
        this.比較行更新保留中か=false;

        var 新規実行選択肢 = this.実行選択肢一覧.FirstOrDefault(新規実行選択肢か);
        this.未保存ベンチマーク結果を破棄();
        this.選択実行読込要求を無効化();
        this.新規実行用に全現在実行結果をクリア();
        this.未保存ベンチマーク結果ありか=false;
        this.終了状態保存保留ありか=false;
        this.現在未保存実行ルート=null;
        if(新規実行選択肢 is not null) {
            this.表示を維持して選択実行選択肢を設定(新規実行選択肢,true);
            this.ピッカー用選択実行選択肢変更を通知();
        }
        this.新規グリッドカタログ表示を復元();
        this.状態文字列="新規グリッドにしました";
    }

    private void 新規グリッドカタログ表示を復元() {
        this.ベンチマーク行選択状態.Clear();
        if(this.カタログケース一覧.Count==0) {
            _=this.更新Async();
            return;
        }

        if(!this.現在カタログ行形状か()) {
            this.カタログからベンチマーク行を再構築();
        }
        this.表示結果を更新();
        this.選択要素数を表へ適用();
        this.新規グリッド用に全ベンチマーク選択をクリア();
        this.ベンチマーク結果表示をクリア();
        this.改善プロンプトを更新();
        this.ソースタブ進行を更新();
        this.現在ソートを適用();
        this.OnPropertyChanged(nameof(this.ベンチマーク行一覧));
    }

    private bool 現在カタログ行形状か() {
        var 要素数 = this.選択結果要素数?.値??0;
        foreach(var 状態 in this.ソースタブ表示状態一覧値) {
            var 期待行一覧 = ベンチマーク行グループ化ロジック.行を作成(this.カタログケース一覧,状態.定義,要素数).Count;
            if(状態.行一覧.Count!=期待行一覧) {
                return false;
            }
        }

        return true;
    }

    private void 新規グリッド用に全ベンチマーク選択をクリア() {
        this.ベンチマーク選択適用中か=true;
        try {
            foreach(var 行 in this.全ベンチマーク行を取得()) {
                行.選択中か=false;
            }
            this.ベンチマーク行選択状態.Clear();
        } finally {
            this.ベンチマーク選択適用中か=false;
        }
        this.ベンチマーク選択集計を更新();
        this.ベンチマーク選択表示を更新();
    }

    /// <summary>
    /// 実行を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 実行を切り替え() {
        _=this.実行または再開Async();
    }

    private void 表示実行中結果をグリッドセルへ適用(
        ベンチマーク結果項目 結果,
        CPU選択項目? CPU,
        DateTimeOffset 観測日時,
        bool 自動スクロールを要求するか,
        bool 完了強調を維持するか) {
        var 行 = this.ベンチマーク行を検索(結果);
        if(行 is null) {
            this.実行中グリッド適用失敗を書き込む("row-not-found",結果,null);
            if(自動スクロールを要求するか) {
                this.ベンチマーク自動スクロールを要求(結果.ソースタブ,結果.メソッド,string.Empty);
            }
            return;
        }

        if(行.プロジェクトセルを取得(結果.プロジェクト) is null) {
            this.実行中グリッド適用失敗を書き込む("cell-not-found",結果,行);
            if(自動スクロールを要求するか) {
                this.ベンチマーク自動スクロールを要求(結果.ソースタブ,結果.メソッド,行.行キー);
            }
            return;
        }

        if(自動スクロールを要求するか) {
            this.ベンチマーク自動スクロールを要求(結果.ソースタブ,結果.メソッド,行.行キー);
        }
        if(完了強調を維持するか) {
            this.実行要素の完了強調を維持(結果.軸数);
        }
        行.実行結果を適用(結果,CPU,観測日時);
    }

    private void 実行中グリッド適用失敗を書き込む(string 理由,ベンチマーク結果項目 結果,ベンチマーク表行? 行) {
        起動診断.書き込む(
            "実行中グリッドセルの適用に失敗しました。 "
            +"理由="+理由+" "
            +"ソースタブ="+結果.ソースタブ+" "
            +"プロジェクト="+結果.プロジェクト+" "
            +"型="+(string.IsNullOrWhiteSpace(結果.型完全名) ? "-" : 結果.型完全名)+" "
            +"メソッド="+結果.メソッド+" "
            +"引数キー="+(string.IsNullOrWhiteSpace(結果.引数キー) ? "-" : 結果.引数キー)+" "
            +"軸="+結果.軸数.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"実行項目インデックス="+結果.実行項目インデックス.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"行="+(行?.行キー??"-")+" "
            +"左プロジェクト="+(行?.ソースタブ.左プロジェクト??"-")+" "
            +"右プロジェクト="+(行?.ソースタブ.右プロジェクト??"-"));
    }

    /// <summary>
    /// ベンチマーク自動スクロールを要求 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="メソッド名">メソッド名 を指定して実行条件を制御します。</param>
    /// <param name="行キー">行キー を指定して実行条件を制御します。</param>
    private void ベンチマーク自動スクロールを要求(ベンチマークソースタブ ソースタブ,string メソッド名,string 行キー) {
        this.最新ベンチマーク自動スクロール要求=new ベンチマーク自動スクロール要求(
            ソースタブ,
            メソッド名,
            行キー,
            unchecked(++this.ベンチマーク自動スクロール連番));
        this.OnPropertyChanged(nameof(this.最新ベンチマーク自動スクロール要求));
    }

    /// <summary>
    /// 結果行キーを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string 結果行キーを作成(ベンチマーク結果項目 結果) =>
        this.ベンチマーク行を検索(結果)?.行キー??結果.メソッド;

    private IReadOnlyList<ベンチマーク結果項目> 全グリッド結果項目を取得() {
        var 現在結果一覧 = this.現在実行セッション.全結果を取得();
        if(現在結果一覧.Count==0) {
            return this.結果項目一覧.ToArray();
        }
        if(this.結果項目一覧.Count==0) {
            return 現在結果一覧;
        }

        return this.結果項目一覧.Concat(現在結果一覧).ToArray();
    }

    /// <summary>
    /// データグリッド更新バッチ深度 フィールドです。
    /// </summary>
    private int データグリッド更新バッチ深度;

    /// <summary>
    /// データグリッド更新バッチ変更時 イベントです。
    /// </summary>
    internal event EventHandler<ベンチマークデータグリッド更新バッチイベント引数>? データグリッド更新バッチ変更時;

    /// <summary>
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// 必 要 に 応 じ て 副 作 用 と し て 内 部 状 態 や 表 示 へ 反 映 し ま す 。
    /// </summary>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    private IDisposable データグリッド更新バッチを開始() {
        var 通知するべきか = this.データグリッド更新バッチ深度==0;
        this.データグリッド更新バッチ深度++;
        if(通知するべきか) {
            this.データグリッド更新バッチ変更時?.Invoke(this,ベンチマークデータグリッド更新バッチイベント引数.開始);
        }

        return new データグリッド更新バッチスコープ(this);
    }

    /// <summary>
    /// データグリッド更新バッチを終了 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void データグリッド更新バッチを終了() {
        if(this.データグリッド更新バッチ深度<=0) {
            return;
        }

        this.データグリッド更新バッチ深度--;
        if(this.データグリッド更新バッチ深度==0) {
            this.データグリッド更新バッチ変更時?.Invoke(this,ベンチマークデータグリッド更新バッチイベント引数.終了);
        }
    }

    /// <summary>
    /// IDisposable プロパティです。
    /// </summary>
    /// <value>IDisposable の値を取得/設定します。</value>
    private sealed class データグリッド更新バッチスコープ(実行ビューモデル 所有者):IDisposable {
        private          bool     破棄済みか;
        public void Dispose() {
            if(this.破棄済みか) {
                return;
            }
            this.破棄済みか=true;
            所有者.データグリッド更新バッチを終了();
        }
    }

    /// <summary>
    /// ソースタブ進行を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ソースタブ進行を更新() {
        if(this.進行更新バッチ深度>0) {
            this.進行更新保留中か=true;
            return;
        }
        this.ソースタブ進行を更新本体();
    }

    /// <summary>
    /// 実行中結果用ソースタブ進行を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 実行中結果用ソースタブ進行を更新() {
        if(this.進行更新バッチ深度>0) {
            this.進行更新保留中か=true;
            return;
        }

        var 現在日時 = DateTimeOffset.UtcNow;
        if(現在日時-this.最終実行中進行更新日時<実行中進行更新間隔) {
            return;
        }

        this.最終実行中進行更新日時=現在日時;
        this.ソースタブ進行を更新本体();
    }

    // 全5タブ分の進捗計算は実行が進むほど重くなる。UIスレッドに残すのは セル一覧 構築(行数程度)と
    // 生の結果配列の確保(参照コピー)のみで、measurement射影(GroupBy/重複排除)と
    // ベンチマークタブ進行ロジック.進行を計算(O(セル一覧×measurements)) はバックグラウンドで実行する。
    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void ソースタブ進行を更新本体() {
        if(this.ソースタブ進行更新中か) {
            this.ソースタブ進行更新保留中か=true;
            return;
        }

        if(this.カタログケース一覧.Count==0) {
            foreach(var 選択肢 in this.ソースタブ一覧) {
                選択肢.進行を設定(new ベンチマークタブ進行(0,0));
            }
            this.全体ソースタブ進行=new ベンチマークタブ進行(0,0);
            return;
        }

        var 入力一覧 = this.ソースタブ一覧
            .Select(選択肢 => {
                var 測定済み = this.測定済み選択セル数を計算(選択肢.定義);
                return new ソースタブ進行入力(
                    選択肢,
                    選択肢.定義,
                    this.進行セル一覧を作成(選択肢.定義),
                    this.現在実行結果項目一覧を取得(選択肢.定義.タブ),
                    測定済み.測定済み,
                    測定済み.選択済み);
            })
            .ToArray();
        var 結果項目一覧スナップショット = this.結果項目一覧.ToArray();
        var 状態ジョブ一覧スナップショット = this.現在実行セッション.結果ありか
            ? null
            : this.選択実行状態を読み込む()?.ジョブ一覧.ToArray();
        var 最大要素数 = this.選択要素数一覧を取得().LastOrDefault();
        var 閾値ナノ秒 = this.適応閾値を取得().TotalMilliseconds*1_000_000;
        var トークン = ++this.ソースタブ進行要求トークン;
        var 世代 = this.実行結果世代;
        this.ソースタブ進行更新中か=true;
        var ストップウォッチ = Stopwatch.StartNew();
        Task.Run(() => 入力一覧
            .Select(入力 => ソースタブ進行を計算(
                    入力.セル一覧,
                    進行測定一覧を構築(入力.セッション結果一覧,結果項目一覧スナップショット,状態ジョブ一覧スナップショット,入力.定義.タブ),
                    入力.定義.左プロジェクト,
                    入力.定義.右プロジェクト,
                    最大要素数,
                    閾値ナノ秒,
                    入力.測定済み選択値セル一覧,
                    入力.選択値セル一覧))
                .ToArray())
            .ContinueWith(タスク => {
                UIスレッド予算.記録("bg:progress",ストップウォッチ.Elapsed.TotalMilliseconds);
                this.UIディスパッチャー.呼び出し開始(() => {
                    this.ソースタブ進行更新中か=false;
                    if(トークン==this.ソースタブ進行要求トークン&&世代==this.実行結果世代&&!タスク.IsFaulted) {
                        var 進行一覧 = タスク.Result;
                        UIスレッド予算.計測("progress-apply",() => {
                            for(var インデックス = 0;インデックス<入力一覧.Length;インデックス++) {
                                入力一覧[インデックス].選択肢.進行を設定(進行一覧[インデックス]);
                                if(進行一覧[インデックス].合計数>0
                                    &&進行一覧[インデックス].完了数>=進行一覧[インデックス].合計数) {
                                    if(!this.ベンチマーク実行有効か) {
                                        入力一覧[インデックス].選択肢.最終完了状態を設定(true);
                                    }
                                }
                            }
                            this.全体ソースタブ進行=new ベンチマークタブ進行(
                                進行一覧.Sum(static 進行 => 進行.完了数),
                                進行一覧.Sum(static 進行 => 進行.合計数));
                        });
                    } else if(タスク.IsFaulted) {
                        起動診断.書き込む("バックグラウンドでのソースタブ進行更新に失敗しました。",タスク.Exception);
                    }

                    if(this.ソースタブ進行更新保留中か) {
                        this.ソースタブ進行更新保留中か=false;
                        this.ソースタブ進行を更新本体();
                    }
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// 進行セルを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="選択中か">選択中か を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static ベンチマークタブ進行セル 進行セルを作成(ベンチマーク行定義 行,string プロジェクト,bool 選択中か) {
        行.プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク);
        return new ベンチマークタブ進行セル(
            行.メソッド名,
            プロジェクト,
            ベンチマーク?.型完全名??string.Empty,
            ベンチマーク?.引数キー??string.Empty,
            選択中か,
            行.行キー);
    }

    private static ベンチマークケース? 保存ケースを取得(ベンチマークケース? 保存ケース) =>
        保存ケース is not null&&!string.IsNullOrWhiteSpace(保存ケース.型完全名) ? 保存ケース : null;

    private void ソースタブ進行を同期適用() {
        _=unchecked(++this.ソースタブ進行要求トークン);
        this.ソースタブ進行更新保留中か=false;
        var 結果項目一覧スナップショット = this.結果項目一覧.ToArray();
        var 状態ジョブ一覧スナップショット = this.現在実行セッション.結果ありか
            ? null
            : this.選択実行状態を読み込む()?.ジョブ一覧.ToArray();
        var 最大要素数 = this.選択要素数一覧を取得().LastOrDefault();
        var 閾値ナノ秒 = this.適応閾値を取得().TotalMilliseconds*1_000_000;
        var 進行一覧 = this.ソースタブ一覧
            .Select(選択肢 => {
                var 測定済み = this.測定済み選択セル数を計算(選択肢.定義);
                return new {
                    選択肢 = 選択肢,
                    進行 = ソースタブ進行を計算(
                        this.進行セル一覧を作成(選択肢.定義),
                        進行測定一覧を構築(
                            this.現在実行結果項目一覧を取得(選択肢.定義.タブ),
                            結果項目一覧スナップショット,
                            状態ジョブ一覧スナップショット,
                            選択肢.定義.タブ),
                        選択肢.定義.左プロジェクト,
                        選択肢.定義.右プロジェクト,
                        最大要素数,
                        閾値ナノ秒,
                        測定済み.測定済み,
                        測定済み.選択済み),
                };
            })
            .ToArray();

        foreach(var 項目 in 進行一覧) {
            項目.選択肢.進行を設定(項目.進行);
        }
        this.全体ソースタブ進行=new ベンチマークタブ進行(
            進行一覧.Sum(static 項目 => 項目.進行.完了数),
            進行一覧.Sum(static 項目 => 項目.進行.合計数));
    }

    private static ベンチマークタブ進行 ソースタブ進行を計算(
        IEnumerable<ベンチマークタブ進行セル> セル一覧,
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string 左プロジェクト,
        string 右プロジェクト,
        int 最大要素数,
        double 閾値ナノ秒,
        int 測定済み選択値セル一覧,
        int 選択値セル一覧) {
        var 進行 = ベンチマークタブ進行ロジック.進行を計算(
            セル一覧,
            測定一覧,
            左プロジェクト,
            右プロジェクト,
            最大要素数,
            閾値ナノ秒);
        return 測定セル進行フォールバックを適用(進行,測定済み選択値セル一覧,選択値セル一覧);
    }

    private static bool タブ最終完了か(ベンチマーク要素測定[] 測定一覧,ベンチマーク表行[] 行一覧,int 最大要素数,double 閾値ナノ秒) {
        var セル一覧 = 行一覧
            .Where(static 行 => 行.選択中か)
            .SelectMany(static 行 => 行.プロジェクトセル一覧
                .Where(static セル => セル.ケースありか)
                .Select(セル => 進行セルを作成(行,セル.プロジェクト,true)));
        return 行一覧.FirstOrDefault() is { } 先頭&&ベンチマーク作業軸.ループ回数を使うか(先頭.ソースタブ.タブ)
            ? ベンチマーク行完了ロジック.ループ回数タブ最終完了か(測定一覧,セル一覧,閾値ナノ秒)
            : ベンチマーク行完了ロジック.タブ最終完了か(測定一覧,セル一覧,最大要素数,閾値ナノ秒);
    }

    /// <summary>
    /// 選択要素数の報告行を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="項目">項目 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static 報告行 選択要素数の報告行を作成(ベンチマーク結果項目 項目) =>
        new(
            項目.プロジェクト,
            項目.メソッド,
            項目.型完全名,
            項目.メソッド,
            項目.状態,
            項目.平均,
            string.Empty,
            string.Empty,
            項目.割り当てバイト数値?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
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

    /// <summary>
    /// 比較用報告行を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="ソース結果一覧">ソース結果一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private 報告行[] 比較用報告行を作成(ベンチマークソースタブ定義 ソースタブ,IReadOnlyList<ベンチマーク結果項目> ソース結果一覧) {
        var 選択値ソース結果一覧 = ソース結果一覧
            .Where(結果 => this.結果がソースタブ内か(結果,ソースタブ))
            .ToArray();
        var 行一覧 = 選択値ソース結果一覧
            .Select(選択要素数の報告行を作成)
            .ToList();
        if(スケーラブルベンチマーク名フォーマッター.前回右列を使うか(ソースタブ.タブ)&&!string.IsNullOrWhiteSpace(ソースタブ.右プロジェクト)) {
            foreach(var 結果 in 選択値ソース結果一覧) {
                var 前回 = this.UI実行リポジトリ.前回結果を検索(
                    UI実行アプリ種別を取得(),
                    ソースタブ,
                    結果.メソッド,
                    結果.プロジェクト,
                    結果.型完全名,
                    結果.軸数,
                    結果.開始日時,
                    結果.引数キー,
                    this.有効UI実行ID??this.未保存UI実行ID);
                if(前回 is null) {
                    continue;
                }

                行一覧.Add(報告行を作成(
                    ソースタブ.右プロジェクト,
                    結果.メソッド,
                    結果.型完全名,
                    前回.プロジェクト測定値に変換(),
                    前回.引数キー,
                    前回.引数一覧));
            }
        }

        return 行一覧
            .GroupBy(static 行 => 行.プロジェクト+"|"+行.型完全名+"|"+行.メソッド+"|"+行.引数キー,StringComparer.OrdinalIgnoreCase)
            .Select(static グループ => グループ.Last())
            .ToArray();
    }

    /// <summary>
    /// 報告行を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="型完全名">型完全名 を指定して実行条件を制御します。</param>
    /// <param name="指標一覧">指標一覧 を指定して実行条件を制御します。</param>
    /// <param name="引数キー">引数キー を指定して実行条件を制御します。</param>
    /// <param name="引数一覧">引数一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static 報告行 報告行を作成(
        string プロジェクト,
        string メソッド,
        string 型完全名,
        プロジェクト測定値 指標一覧,
        string 引数キー = "",
        IReadOnlyList<ベンチマーク引数>? 引数一覧 = null) =>
        new(
            プロジェクト,
            メソッド,
            型完全名,
            メソッド,
            指標一覧.測定値ありか ? "Completed" : "Missing",
            指標一覧.時間ナノ秒?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            指標一覧.割り当てバイト数?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            指標一覧.時間ナノ秒??0,
            0,
            0,
            指標一覧.割り当てバイト数??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            指標一覧.測定値ありか ? 0 : null,
            引数キー,
            引数一覧);
}
