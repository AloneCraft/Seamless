using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Seamless.UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// ソート規則の追加・並べ替え・保存
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// ベンチマークソート設定キー フィールドです。
    /// </summary>
    private const string ベンチマークソート設定キー = "Seamless.SortRules.v1";

    /// <summary>
    /// ソート規則適用中か フィールドです。
    /// </summary>
    private bool ソート規則適用中か;

    /// 現在ソート列値 フィールドです。
    /// </summary>
    private ベンチマークグリッドソート列 現在ソート列値 = ベンチマークグリッドソート列.ベンチマークメソッド;

    /// <summary>
    /// 現在ソート降順か値 フィールドです。
    /// </summary>
    private bool 内部現在ソート降順か;

    /// <summary>
    /// 保存ソート規則スナップショット一覧 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークソート規則スナップショット> 保存ソート規則スナップショット一覧 = [];

    /// <summary>
    /// ベンチマークソート規則一覧 プロパティです。
    /// </summary>
    /// <value>ベンチマークソート規則一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマークソート規則> ベンチマークソート規則一覧 { get; } = [];

    /// <summary>
    /// ベンチマークソート列選択肢一覧 プロパティです。
    /// </summary>
    /// <value>ベンチマークソート列選択肢一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマークソート列選択肢> ベンチマークソート列選択肢一覧 { get; } = [];

    /// <summary>
    /// 現在ソート列 プロパティです。
    /// </summary>
    /// <value>現在ソート列 の値を取得/設定します。</value>
    public ベンチマークグリッドソート列 現在ソート列 {
        get => this.現在ソート列値;
        private set => this.SetProperty(ref this.現在ソート列値,value);
    }

    /// <summary>
    /// 現在ソート降順か プロパティです。
    /// </summary>
    /// <value>現在ソート降順か の値を取得/設定します。</value>
    public bool 現在ソート降順か {
        get => this.内部現在ソート降順か;
        private set => this.SetProperty(ref this.内部現在ソート降順か,value);
    }

    /// <summary>
    /// ベンチマークソート概要文字列 プロパティです。
    /// </summary>
    /// <value>ベンチマークソート概要文字列 の値を取得/設定します。</value>
    public string ベンチマークソート概要文字列 =>
        this.ベンチマークソート規則一覧.Count==0
            ? "既定"
            : string.Join(" > ",this.ベンチマークソート規則一覧.Select(static 規則 => 規則.列文字列+" "+規則.方向文字列));

    // 実行中は行位置を固定するため、ソートルールの編集(追加/削除/移動/方向切替)をUIから禁止する。
    /// <summary>
    /// ソート規則編集可能か プロパティです。
    /// </summary>
    /// <value>ソート規則編集可能か の値を取得/設定します。</value>
    public bool ソート規則編集可能か =>
        !this.ベンチマーク一時停止中か
        &&(!this.ベンチマーク実行有効か||this.選択ソース定義.タブ!=this.有効実行ソースタブ);

    /// <summary>
    /// ベンチマーク行をソート メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="列">列 を指定して実行条件を制御します。</param>
    public void ベンチマーク行をソート(ベンチマークグリッドソート列 列) {
        var 降順か = false;
        if(this.現在ソート列==列&&this.ベンチマークソート規則一覧.Count==1) {
            降順か=!this.現在ソート降順か;
        }

        this.ベンチマークソート規則一覧を設定([new ベンチマークソート規則(列,降順か)],true);
    }

    /// <summary>
    /// ベンチマークソート規則を追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void ベンチマークソート規則を追加() {
        var 選択肢 = this.ベンチマークソート列選択肢一覧
            .FirstOrDefault(選択肢 => this.ベンチマークソート規則一覧.All(規則 => 規則.列!=選択肢.列))
            ??this.ベンチマークソート列選択肢一覧.FirstOrDefault();
        if(選択肢 is null) {
            return;
        }

        var 規則 = new ベンチマークソート規則(選択肢.列);
        this.ベンチマークソート規則を購読(規則);
        this.ベンチマークソート規則一覧.Add(規則);
        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則を削除 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則">規則 を指定して実行条件を制御します。</param>
    public void ベンチマークソート規則を削除(ベンチマークソート規則 規則) {
        this.ベンチマークソート規則一覧.Remove(規則);
        if(this.ベンチマークソート規則一覧.Count==0) {
            this.ベンチマークソート規則を既定へ戻す(true);
            return;
        }

        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則を移動 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="ソース">ソース を指定して実行条件を制御します。</param>
    /// <param name="対象">対象 を指定して実行条件を制御します。</param>
    public void ベンチマークソート規則を移動(ベンチマークソート規則 ソース,ベンチマークソート規則 対象) {
        var 旧インデックス = this.ベンチマークソート規則一覧.IndexOf(ソース);
        var 新規インデックス = this.ベンチマークソート規則一覧.IndexOf(対象);
        if(旧インデックス<0||新規インデックス<0||旧インデックス==新規インデックス) {
            return;
        }

        this.ベンチマークソート規則一覧.Move(旧インデックス,新規インデックス);
        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則を上へ移動 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則">規則 を指定して実行条件を制御します。</param>
    public void ベンチマークソート規則を上へ移動(ベンチマークソート規則 規則) {
        var インデックス = this.ベンチマークソート規則一覧.IndexOf(規則);
        if(インデックス<=0) {
            return;
        }

        this.ベンチマークソート規則一覧.Move(インデックス,インデックス-1);
        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則を下へ移動 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則">規則 を指定して実行条件を制御します。</param>
    public void ベンチマークソート規則を下へ移動(ベンチマークソート規則 規則) {
        var インデックス = this.ベンチマークソート規則一覧.IndexOf(規則);
        if(インデックス<0||インデックス>=this.ベンチマークソート規則一覧.Count-1) {
            return;
        }

        this.ベンチマークソート規則一覧.Move(インデックス,インデックス+1);
        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則列を設定 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則">規則 を指定して実行条件を制御します。</param>
    /// <param name="列">列 を指定して実行条件を制御します。</param>
    public void ベンチマークソート規則列を設定(ベンチマークソート規則 規則,ベンチマークグリッドソート列 列) {
        規則.列=列;
        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則方向を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則">規則 を指定して実行条件を制御します。</param>
    public void ベンチマークソート規則方向を切り替え(ベンチマークソート規則 規則) {
        規則.降順か=!規則.降順か;
        this.ソート規則変更を適用(true);
    }

    /// <summary>
    /// ベンチマークソート規則をリセット メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void ベンチマークソート規則をリセット() =>
        this.ベンチマークソート規則を既定へ戻す(true);

    /// <summary>
    /// ベンチマークソート規則一覧を設定 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則一覧">規則一覧 を指定して実行条件を制御します。</param>
    /// <param name="保存するか">保存するか を指定して実行条件を制御します。</param>
    private void ベンチマークソート規則一覧を設定(IEnumerable<ベンチマークソート規則> 規則一覧,bool 保存するか) {
        this.ソート規則適用中か=true;
        try {
            this.ベンチマークソート規則一覧.Clear();
            foreach(var 規則 in 規則一覧) {
                this.ベンチマークソート規則を購読(規則);
                this.ベンチマークソート規則一覧.Add(規則);
            }
        } finally {
            this.ソート規則適用中か=false;
        }
        this.ソート規則変更を適用(保存するか);
    }

    /// <summary>
    /// ベンチマークソート規則を購読 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="規則">規則 を指定して実行条件を制御します。</param>
    private void ベンチマークソート規則を購読(ベンチマークソート規則 規則) {
        規則.PropertyChanged+=(_,引数) => {
            if(this.ソート規則適用中か) {
                return;
            }
            if(引数.PropertyName==nameof(ベンチマークソート規則.列)
                ||引数.PropertyName==nameof(ベンチマークソート規則.降順か)) {
                this.ソート規則変更を適用(true);
            }
        };
    }

    /// <summary>
    /// ソート規則変更を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="保存するか">保存するか を指定して実行条件を制御します。</param>
    private void ソート規則変更を適用(bool 保存するか) {
        this.現在ソート表示を更新();
        this.OnPropertyChanged(nameof(this.ベンチマークソート概要文字列));
        this.現在ソートを適用();
        if(保存するか) {
            this.ベンチマークソート規則を保存();
        }
    }

    /// <summary>
    /// ベンチマークソート列を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークソート列を更新() {
        this.ベンチマークソート列選択肢を更新();
        var 規則一覧 = this.有効ソート規則を作成(this.ベンチマークソート規則一覧).ToArray();
        if(規則一覧.Length==0) {
            規則一覧=this.初期ソート規則を作成().ToArray();
        }
        this.ベンチマークソート規則一覧を設定(規則一覧,false);
    }

    /// <summary>
    /// ベンチマークソート列選択肢を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークソート列選択肢を更新() {
        var 列一覧 = this.利用可能ソート列を作成().ToArray();
        this.ベンチマークソート列選択肢一覧.Clear();
        foreach(var 列 in 列一覧) {
            this.ベンチマークソート列選択肢一覧.Add(new ベンチマークソート列選択肢(列,ベンチマークソート規則.列文字列を取得(列)));
        }
    }

    /// <summary>
    /// 利用可能ソート列を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<ベンチマークグリッドソート列> 利用可能ソート列を作成() {
        yield return ベンチマークグリッドソート列.ベンチマークメソッド;
        yield return ベンチマークグリッドソート列.論理CPU;
        foreach(var 列 in ベンチマーク引数列一覧.取得(this.選択ソース定義)) {
            yield return 列 switch {
                スケーラブルベンチマーク引数列.Count => ベンチマークグリッドソート列.引数Count,
                スケーラブルベンチマーク引数列.Index => ベンチマークグリッドソート列.引数インデックス,
                スケーラブルベンチマーク引数列.Page => ベンチマークグリッドソート列.引数ページ,
                スケーラブルベンチマーク引数列.Segment => ベンチマークグリッドソート列.引数セグメント,
                スケーラブルベンチマーク引数列.Memory => ベンチマークグリッドソート列.引数メモリ,
                スケーラブルベンチマーク引数列.Fraction => ベンチマークグリッドソート列.引数割合,
                スケーラブルベンチマーク引数列.Initial => ベンチマークグリッドソート列.引数初期値,
                スケーラブルベンチマーク引数列.Data => ベンチマークグリッドソート列.引数データ,
                _ => ベンチマークグリッドソート列.ベンチマークメソッド,
            };
        }
        yield return ベンチマークグリッドソート列.左日時;
        yield return ベンチマークグリッドソート列.左時間;
        yield return ベンチマークグリッドソート列.左割り当て;
        yield return ベンチマークグリッドソート列.時間シェア;
        yield return ベンチマークグリッドソート列.メモリシェア;
        yield return ベンチマークグリッドソート列.右日時;
        yield return ベンチマークグリッドソート列.右時間;
        yield return ベンチマークグリッドソート列.右割り当て;
    }

    /// <summary>
    /// 有効ソート規則を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソース">ソース を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークソート規則> 有効ソート規則を作成(IEnumerable<ベンチマークソート規則> ソース) {
        var 許可済み = this.利用可能ソート列を作成().ToHashSet();
        var 使用済み = new HashSet<ベンチマークグリッドソート列>();
        return ソース
            .Where(規則 => 許可済み.Contains(規則.列)&&使用済み.Add(規則.列))
            .Select(規則 => new ベンチマークソート規則(規則.列,規則.降順か))
            .ToArray();
    }

    /// <summary>
    /// 初期ソート規則を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークソート規則> 初期ソート規則を作成() {
        var 復元 = this.有効ソート規則を作成(this.保存ソート規則スナップショット一覧.Select(static 項目 => new ベンチマークソート規則(項目.列,項目.降順か)));
        if(復元.Count>0) {
            return 復元;
        }

        return this.既定ソート規則を作成();
    }

    /// <summary>
    /// 既定ソート規則を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークソート規則> 既定ソート規則を作成() =>
        [new ベンチマークソート規則(ベンチマークグリッドソート列.ベンチマークメソッド)];

    /// <summary>
    /// ベンチマークソート規則を既定へ戻す メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="保存するか">保存するか を指定して実行条件を制御します。</param>
    private void ベンチマークソート規則を既定へ戻す(bool 保存するか) =>
        this.ベンチマークソート規則一覧を設定(this.既定ソート規則を作成(),保存するか);

    /// <summary>
    /// 有効ソート規則を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークソート規則> 有効ソート規則を取得() {
        var 規則一覧 = this.有効ソート規則を作成(this.ベンチマークソート規則一覧);
        return 規則一覧.Count>0 ? 規則一覧 : this.既定ソート規則を作成();
    }

    /// <summary>
    /// 現在ソート表示を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 現在ソート表示を更新() {
        var 先頭 = this.有効ソート規則を取得().FirstOrDefault();
        if(先頭 is null) {
            this.現在ソート列=ベンチマークグリッドソート列.ベンチマークメソッド;
            this.現在ソート降順か=false;
            return;
        }

        this.現在ソート列=先頭.列;
        this.現在ソート降順か=先頭.降順か;
    }

    /// <summary>
    /// 保存ベンチマークソート規則を読み込む メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマークソート規則スナップショット> 保存ベンチマークソート規則を読み込む() {
        try {
            var JSON = this.UI設定.取得(ベンチマークソート設定キー,string.Empty);
            return string.IsNullOrWhiteSpace(JSON)
                ? []
                : JsonSerializer.Deserialize<ベンチマークソート規則スナップショット[]>(JSON)?? [];
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマークソート設定の読み込みに失敗しました。",例外);
            return [];
        }
    }

    /// <summary>
    /// ベンチマークソート規則を保存 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークソート規則を保存() {
        try {
            var スナップショット = this.ベンチマークソート規則一覧
                .Select(static 規則 => new ベンチマークソート規則スナップショット(規則.列,規則.降順か))
                .ToArray();
            this.保存ソート規則スナップショット一覧=スナップショット;
            this.UI設定.設定(ベンチマークソート設定キー,JsonSerializer.Serialize(スナップショット));
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマークソート設定の保存に失敗しました。",例外);
        }
    }

    /// <summary>
    /// 現在ソートを適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 現在ソートを適用() {
        if(this.ベンチマーク一時停止中か) {
            // 一時停止中は再開対象の行位置を保持するため、並び替えを行わない。
            return;
        }
        using(this.データグリッド更新バッチを開始()) {
            var ソート規則一覧 = this.有効ソート規則を取得();
            foreach(var 状態 in this.ソースタブ表示状態一覧値) {
                if(this.ベンチマーク実行有効か&&状態.定義.タブ==this.有効実行ソースタブ) {
                    // 実行対象タブだけ行位置を固定する（インデックス主導更新の前提）。
                    continue;
                }
                if(状態.行一覧.Count<2) {
                    continue;
                }

                var 並び替え済み = 状態.行一覧
                    .OrderBy(行 => 行,Comparer<ベンチマーク表行>.Create((左,右) => {
                        foreach(var 規則 in ソート規則一覧) {
                            var 結果 = ベンチマークソート値.比較(
                                左.ソート値を取得(規則.列),
                                右.ソート値を取得(規則.列),
                                規則.降順か);
                            if(結果!=0) {
                                return 結果;
                            }
                        }

                        var 型結果 = string.Compare(左.ベンチマーク型文字列,右.ベンチマーク型文字列,StringComparison.OrdinalIgnoreCase);
                        if(型結果!=0) {
                            return 型結果;
                        }
                        var メソッド結果 = string.Compare(左.ベンチマークメソッド文字列,右.ベンチマークメソッド文字列,StringComparison.OrdinalIgnoreCase);
                        return メソッド結果!=0
                            ? メソッド結果
                            : string.Compare(左.行キー,右.行キー,StringComparison.OrdinalIgnoreCase);
                    }))
                    .ToArray();
                状態.行一覧.範囲を置換(並び替え済み);
            }
        }
    }

    /// <summary>
    /// 最終ベンチマークソートを適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 最終ベンチマークソートを適用() {
        this.ベンチマークソート規則一覧を設定([new ベンチマークソート規則(ベンチマークグリッドソート列.時間シェア,true)],true);
    }

    /// <summary>
    /// ベンチマークソート規則スナップショット メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="列">列 を指定して実行条件を制御します。</param>
    /// <param name="降順か">降順か を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct ベンチマークソート規則スナップショット(ベンチマークグリッドソート列 列,bool 降順か);
}
