using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Seamless.UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// ソースタブの選択と表示
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// ソースタブ表示状態一覧値 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークソースタブ表示状態> ソースタブ表示状態一覧値 = [];

    /// <summary>
    /// タブ別ソースタブ表示状態 フィールドです。
    /// </summary>
    private readonly Dictionary<ベンチマークソースタブ,ベンチマークソースタブ表示状態> タブ別ソースタブ表示状態 = [];

    /// <summary>
    /// ソースタブ実行中状態 フィールドです。
    /// </summary>
    private readonly ベンチマークソースタブ実行中状態 ソースタブ実行中状態 = new();

    /// <summary>
    /// 有効実行ソースタブ値 フィールドです。
    /// </summary>
    private ベンチマークソースタブ? 有効実行ソースタブ値;

    /// <summary>
    /// ソースタブ表示状態一覧 プロパティです。
    /// </summary>
    /// <value>ソースタブ表示状態一覧 の値を取得/設定します。</value>
    public IReadOnlyList<ベンチマークソースタブ表示状態> ソースタブ表示状態一覧 => this.ソースタブ表示状態一覧値;

    /// <summary>
    /// 有効実行ソースタブ プロパティです。
    /// </summary>
    /// <value>有効実行ソースタブ の値を取得/設定します。</value>
    public ベンチマークソースタブ? 有効実行ソースタブ {
        get => this.有効実行ソースタブ値;
        private set {
            if(this.SetProperty(ref this.有効実行ソースタブ値,value)) {
                this.OnPropertyChanged(nameof(this.ベンチマーク実行有効か));
                this.OnPropertyChanged(nameof(this.ソート規則編集可能か));
                this.OnPropertyChanged(nameof(this.ベンチマーク選択を変更できるか));
                this.OnPropertyChanged(nameof(this.実行ボタン文字列));
                this.OnPropertyChanged(nameof(this.ベンチマーク一時停止中か));
                this.OnPropertyChanged(nameof(this.ベンチマーク条件を変更可能か));
                this.OnPropertyChanged(nameof(this.実行履歴を選択可能か));
                this.OnPropertyChanged(nameof(this.ベンチマークを再開可能か));
                this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
                this.OnPropertyChanged(nameof(this.DB保存可能か));
                this.OnPropertyChanged(nameof(this.新規グリッド作成可能か));
                this.実行履歴クリアコマンド.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.実行履歴をクリア可能か));
                this.OnPropertyChanged(nameof(this.実行履歴クリアツールチップ文字列));
                this.OnPropertyChanged(nameof(this.ツールバー状態入力));
            }
        }
    }

    /// <summary>
    /// ソースタブ表示状態を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    public ベンチマークソースタブ表示状態 ソースタブ表示状態を取得(ベンチマークソースタブ ソースタブ) =>
        this.タブ別ソースタブ表示状態.TryGetValue(ソースタブ,out var 状態)
            ? 状態
            : this.ソースタブ表示状態一覧値[0];

    /// <summary>
    /// 行の引数と一致するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="期待引数キー">期待引数キー を指定して実行条件を制御します。</param>
    /// <param name="実際引数キー">実際引数キー を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 行の引数と一致するか(ベンチマークソースタブ ソースタブ,string 期待引数キー,string 実際引数キー) =>
        !スケーラブルソースタブか(ソースタブ)
        ||string.Equals(期待引数キー,実際引数キー,StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// スケーラブルソースタブか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool スケーラブルソースタブか(ベンチマークソースタブ ソースタブ) =>
        ソースタブ is ベンチマークソースタブ.スケーラブルセット
            or ベンチマークソースタブ.スケーラブル辞書
            or ベンチマークソースタブ.スケーラブル永続化リスト
            or ベンチマークソースタブ.スケーラブル永続化セット
            or ベンチマークソースタブ.スケーラブルTsavoriteセット
            or ベンチマークソースタブ.スケーラブル永続化辞書
            or ベンチマークソースタブ.スケーラブルTsavorite辞書;

    /// <summary>
    /// 有効実行ソースタブを記録 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    private void 有効実行ソースタブを記録(ベンチマークソースタブ ソースタブ) {
        this.有効実行ソースタブ=ソースタブ;
    }

    /// <summary>
    /// ソースタブ実行中結果を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    private void ソースタブ実行中結果を適用(ベンチマーク結果項目 結果) {
        this.ソースタブ実行中状態.適用(結果);
        if(!実行中結果か(結果)) {
            return;
        }

        var 選択肢 = this.ソースタブ一覧.FirstOrDefault(候補 => 候補.タブ==結果.ソースタブ);
        選択肢?.実行中状態を設定(true);
    }

    /// <summary>
    /// ソースタブ実行中状態をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ソースタブ実行中状態をクリア() {
        if(this.ソースタブ実行中状態.クリア()) {
            this.ソースタブ実行中状態を更新();
            return;
        }

        this.ソースタブ実行中状態を更新();
    }

    /// <summary>
    /// ソースタブ実行中状態を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ソースタブ実行中状態を更新() {
        foreach(var 選択肢 in this.ソースタブ一覧) {
            選択肢.実行中状態を設定(this.ソースタブ実行中状態.実行中か(選択肢.タブ));
        }
    }

    /// <summary>
    /// 結果確認を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="ソースタブ値">ソースタブ値 を指定して実行条件を制御します。</param>
    private void 結果確認を更新(ベンチマークソースタブ ソースタブ値) {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ソースタブ値);
        var 結果一覧 = this.現在実行結果項目一覧を取得(ソースタブ.タブ)
            .Concat(this.結果項目一覧.Where(項目 => 項目.ソースタブ==ソースタブ.タブ))
            .Where(static 項目 => 項目.軸数>0)
            .GroupBy(項目 => this.結果行キーを作成(項目)+"|"+項目.軸数.ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparer.OrdinalIgnoreCase);
        foreach(var グループ in 結果一覧) {
            var 左 = グループ.LastOrDefault(項目 => string.Equals(項目.プロジェクト,ソースタブ.左プロジェクト,StringComparison.OrdinalIgnoreCase));
            var 右 = グループ.LastOrDefault(項目 => string.Equals(項目.プロジェクト,ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase));
            if(左 is null||右 is null
                ||string.IsNullOrWhiteSpace(左.結果ダイジェスト)
                ||string.IsNullOrWhiteSpace(右.結果ダイジェスト)) {
                continue;
            }

            var 確認文字列 = string.Equals(左.結果ダイジェスト,右.結果ダイジェスト,StringComparison.Ordinal) ? "OK" : "NG";
            this.結果確認を適用(左,確認文字列);
            this.結果確認を適用(右,確認文字列);
        }
    }

    private bool 表示に今回実行メモリを使うか(ベンチマークソースタブ定義 ソースタブ) =>
        !保存DB実行選択肢か(this.選択実行選択肢)
        &&ベンチマーク実行表示元ロジック.今回実行メモリを使うか(this.現在実行セッション.ソースタブ結果があるか(ソースタブ.タブ));

    /// <summary>
    /// 現在実行結果項目一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク結果項目> 現在実行結果項目一覧を取得(ベンチマークソースタブ ソースタブ) =>
        this.現在実行セッション.結果を取得(ソースタブ);

    /// <summary>
    /// 全ソースタブ比較表示をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 全ソースタブ比較表示をクリア() {
        foreach(var 状態 in this.ソースタブ表示状態一覧値) {
            状態.比較行一覧= [];
            状態.ベンチマークシェア概要文=string.Empty;
            this.ソースタブ選択肢を取得(状態.定義.タブ).ベンチマークシェア概要短文を設定(ベンチマーク改善プロンプトビルダー.概要を作成([]).短縮表示文字列);
        }
        this.全体ベンチマークシェア概要を更新();
        this.OnPropertyChanged(nameof(this.ベンチマークシェア概要文));
    }

    /// <summary>
    /// 表示測定一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソース">ソース を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク要素測定[] 表示測定一覧を作成(IReadOnlyList<ベンチマーク結果項目> ソース,ベンチマークソースタブ定義 ソースタブ) {
        var 測定一覧 = new List<ベンチマーク要素測定>(ソース.Count);
        foreach(var 結果 in ソース) {
            if(!this.結果がソースタブ内か(結果,ソースタブ)) {
                continue;
            }

            測定一覧.Add(new ベンチマーク要素測定(
                結果.プロジェクト,
                結果.メソッド,
                結果.型完全名,
                結果.引数キー,
                結果.軸数,
                結果.状態,
                結果.平均ナノ秒,
                結果.合計ナノ秒));
        }

        return 測定一覧.ToArray();
    }

    private bool 結果がソースタブ内か(ベンチマーク結果項目 結果,ベンチマークソースタブ定義 ソースタブ) {
        if(!ソースタブ.プロジェクトを含むか(結果.プロジェクト)) {
            return false;
        }

        return this.ベンチマーク行を検索(結果) is not null;
    }

    private static IReadOnlyList<(string 左プロジェクト,string 右プロジェクト)> ベンチマークペア一覧を作成(ベンチマークソースタブ定義 ソースタブ) =>
        ソースタブ.比較ペア一覧
            .Select(static ペア=>(ペア.左プロジェクト,ペア.右プロジェクト))
            .ToArray();
}
