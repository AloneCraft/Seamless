using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// DB 実行一覧と実行選択肢
public sealed partial class 実行ビューモデル:ObservableObject {
    private static readonly ベンチマーク実行選択肢 新規実行選択肢 = new(
        新規実行番号,
        0,
        "新規",
        string.Empty,
        ベンチマーク実行プロファイル.高速,
        DateTimeOffset.MinValue,
        DateTimeOffset.MinValue,
        0,
        0,
        []);

    /// <summary>
    /// UI実行ストア フィールドです。
    /// </summary>
    private readonly UI実行ストア UI実行ストア;

    /// <summary>
    /// 選択実行選択肢値 フィールドです。
    /// </summary>
    private ベンチマーク実行選択肢? 選択実行選択肢値;

    /// <summary>
    /// 実行選択肢選択適用中か フィールドです。
    /// </summary>
    private bool 実行選択肢選択適用中か;

    /// <summary>
    /// 選択実行DB読込中か フィールドです。
    /// </summary>
    private bool 選択実行DB読込中か;

    /// <summary>
    /// 実行履歴読込中か フィールドです。
    /// </summary>
    private bool 内部実行履歴読込中か;

    /// <summary>
    /// 選択実行読込要求トークン フィールドです。
    /// </summary>
    private int 選択実行読込要求トークン;

    /// <summary>
    /// 読込済み改善選択実行ID フィールドです。
    /// </summary>
    private long? 読込済み改善選択実行ID;

    /// <summary>
    /// 実行選択肢一覧 プロパティです。
    /// </summary>
    /// <value>実行選択肢一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク実行選択肢> 実行選択肢一覧 { get; } = [];

    /// <summary>
    /// 実行履歴読込中か プロパティです。
    /// </summary>
    /// <value>実行履歴読込中か の値を取得/設定します。</value>
    public bool 実行履歴読込中か {
        get => this.内部実行履歴読込中か;
        private set {
            if(this.SetProperty(ref this.内部実行履歴読込中か,value)) {
                this.OnPropertyChanged(nameof(this.実行履歴を選択可能か));
                this.OnPropertyChanged(nameof(this.ベンチマーク条件を変更可能か));
                this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
                this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
                this.OnPropertyChanged(nameof(this.DB保存可能か));
                this.OnPropertyChanged(nameof(this.新規グリッド作成可能か));
                this.実行コマンド.NotifyCanExecuteChanged();
                this.実行切替コマンド.NotifyCanExecuteChanged();
                this.DB保存コマンド.NotifyCanExecuteChanged();
                this.新規グリッドコマンド.NotifyCanExecuteChanged();
#if DEBUG
                this.無限実行コマンド.NotifyCanExecuteChanged();
#endif
            }
        }
    }

    /// <summary>
    /// 実行履歴を選択可能か プロパティです。
    /// </summary>
    /// <value>実行履歴を選択可能か の値を取得/設定します。</value>
    public bool 実行履歴を選択可能か =>
        !this.実行履歴読込中か
        &&!this.更新中か
        &&!this.ベンチマーク実行有効か
        &&!this.ベンチマーク一時停止中か;

    /// <summary>
    /// 実行履歴をクリア可能か プロパティです。
    /// </summary>
    /// <value>実行履歴をクリア可能か の値を取得/設定します。</value>
    public bool 実行履歴をクリア可能か =>
        !this.更新中か
        &&!this.ベンチマーク実行有効か
        &&!this.ベンチマーク一時停止中か;

    public string 実行履歴クリアツールチップ文字列 => this.ベンチマーク実行有効か
        ? "ベンチマーク実行中は履歴を消去できません。"
        : this.ベンチマーク一時停止中か
            ? "ベンチマーク一時停止中は履歴を消去できません。"
            : this.更新中か
                ? "処理中は履歴を消去できません。"
                : "履歴消去";

    /// <summary>
    /// 選択実行選択肢 プロパティです。
    /// </summary>
    /// <value>選択実行選択肢 の値を取得/設定します。</value>
    public ベンチマーク実行選択肢? 選択実行選択肢 {
        get => this.選択実行選択肢参照を正規化();
        set {
            if(this.実行選択肢選択適用中か||this.UI状態適用中か) {
                if(this.SetProperty(ref this.選択実行選択肢値,value)&&!this.UI状態適用中か) {
                    this.選択実行選択肢を適用(value,true);
                    this.UI状態を保存();
                }
                return;
            }

            if(this.選択実行選択肢値==value) {
                return;
            }

            _=this.実行選択肢を選択Async(value);
        }
    }

    /// DB実行一覧を更新Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="キャンセルトークン">キャンセルトークン を指定して実行条件を制御します。</param>
    /// <param name="選択値を読み込むか">選択値を読み込むか を指定して実行条件を制御します。</param>
    private async Task DB実行一覧を更新Async(CancellationToken キャンセルトークン,bool 選択値を読み込むか,long? 優先UI実行ID = null,bool 表示を維持するか = false,bool 新規選択を強制するか = false) {
        var 実行一覧 = await Task.Run(() => {
            キャンセルトークン.ThrowIfCancellationRequested();
            this.UI実行ストア.スキーマを確認();
            return this.UI実行ストア.実行一覧を取得(UI実行アプリ種別を取得())
                .Where(実行 => string.Equals(実行.実行プロファイル,this.選択実行プロファイル.プロファイル.ToString(),StringComparison.OrdinalIgnoreCase))
                .Where(実行 => this.未保存UI実行ID!=実行.実行ID)
                .ToArray();
        },キャンセルトークン);
        if(新規選択を強制するか) {
            this.新規選択用に復元実行再開状態をクリア();
        } else {
            this.最新再開可能実行の自動一時停止を試みる(実行一覧);
        }
        var 選択肢一覧 = 実行一覧.Select(this.実行選択肢を作成).ToArray();
        var 選択値UI実行ID = 優先UI実行ID??this.選択実行選択肢?.UI実行ID;
        var 自動一時停止中実行 = 実行一覧.FirstOrDefault(実行 => 実行.状態 is UI実行状態.実行中 or UI実行状態.待機中 or UI実行状態.一時停止中
            &&this.未完了選択測定があるか(実行.実行ID));
        this.実行選択肢一覧.Clear();
        this.実行選択肢一覧.Add(新規実行選択肢を作成(this.選択実行プロファイル.プロファイル));
        foreach(var 選択肢 in 選択肢一覧) {
            this.実行選択肢一覧.Add(選択肢);
        }
        var 新規実行選択肢 = this.実行選択肢一覧.FirstOrDefault(新規実行選択肢か);
        var 選択肢へ選択 = 新規選択を強制するか
            ? 新規実行選択肢??this.実行選択肢一覧.FirstOrDefault()
            : 自動一時停止中実行 is not null
            ? this.実行選択肢一覧.FirstOrDefault(選択肢 => 選択肢.UI実行ID==自動一時停止中実行.実行ID)??this.実行選択肢一覧.FirstOrDefault()
            : this.実行選択肢一覧.FirstOrDefault(選択肢 => 選択肢.UI実行ID==選択値UI実行ID)??this.実行選択肢一覧.FirstOrDefault();
        if(表示を維持するか||新規選択を強制するか) {
            this.表示を維持して選択実行選択肢を設定(選択肢へ選択,false);
        } else {
            this.選択実行選択肢を設定(
                選択肢へ選択,
                選択値を読み込むか,
                false);
        }
        this.ピッカー用選択実行選択肢変更を通知();
    }

    private async Task DB実行選択肢のみ更新Async(CancellationToken キャンセルトークン,long? 優先UI実行ID = null) {
        var 実行一覧 = await Task.Run(() => {
            キャンセルトークン.ThrowIfCancellationRequested();
            this.UI実行ストア.スキーマを確認();
            return this.UI実行ストア.実行一覧を取得(UI実行アプリ種別を取得())
                .Where(実行 => string.Equals(実行.実行プロファイル,this.選択実行プロファイル.プロファイル.ToString(),StringComparison.OrdinalIgnoreCase))
                .Where(実行 => this.未保存UI実行ID!=実行.実行ID)
                .ToArray();
        },キャンセルトークン);
        this.最新再開可能実行の自動一時停止を試みる(実行一覧);
        var 選択肢一覧 = 実行一覧.Select(this.実行選択肢を作成).ToArray();
        var 現在選択 = this.選択実行選択肢;
        this.実行選択肢一覧.Clear();
        this.実行選択肢一覧.Add(新規実行選択肢を作成(this.選択実行プロファイル.プロファイル));
        foreach(var 選択肢 in 選択肢一覧) {
            this.実行選択肢一覧.Add(選択肢);
        }

        var 置換値 = 優先UI実行ID is { } 実行ID
            ? this.実行選択肢一覧.FirstOrDefault(選択肢 => 選択肢.UI実行ID==実行ID)
            : 現在選択 is null
                ? null
                : this.実行選択肢一覧.FirstOrDefault(選択肢 => 選択肢.UI実行ID==現在選択.UI実行ID)
                    ??(新規実行選択肢か(現在選択) ? this.実行選択肢一覧.FirstOrDefault(新規実行選択肢か) : null);
        if(置換値 is not null&&現在選択 is not null&&置換値.UI実行ID==現在選択.UI実行ID) {
            this.表示を維持して選択実行選択肢を設定(置換値,false);
            this.ピッカー用選択実行選択肢変更を通知();
        }
    }

    private void 最新再開可能実行の自動一時停止を試みる(IReadOnlyList<UI実行記録> 実行一覧) {
        var 最新 = 実行一覧.FirstOrDefault();
        if(最新 is null||最新.状態 is not (UI実行状態.実行中 or UI実行状態.待機中 or UI実行状態.一時停止中)) {
            return;
        }

        if(!this.未完了選択測定があるか(最新.実行ID)) {
            return;
        }

        if(最新.状態!=UI実行状態.一時停止中) {
            this.UI実行ストア.実行状態を設定(最新.実行ID,UI実行状態.一時停止中,"DBから未完了のベンチマーク実行を復元しました。");
        }

        if(this.UI実行ストア.実行設定を取得(最新.実行ID) is { } 設定&&!string.IsNullOrWhiteSpace(設定.実行ルート)) {
            this.有効UI実行ID=最新.実行ID;
            this.有効実行ルートを設定(設定.実行ルート);
        }
    }

    /// <summary>
    /// 実行選択肢を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行">実行 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク実行選択肢 実行選択肢を作成(UI実行記録 実行) {
        var スナップショット項目一覧 = this.UI実行ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID);
        var ソースタブ = Enum.TryParse<ベンチマークソースタブ>(実行.タブ名,true,out var 解析済みソースタブ)
            ? 解析済みソースタブ
            : this.選択ソース定義.タブ;
        var 要素件数一覧 = ベンチマーク要素数一覧.既定初期値.ToArray();
        var 選択側一覧 = スナップショット項目一覧
            .Where(static 項目 => 項目.選択中か)
            .SelectMany(static 項目 => スナップショット側一覧を取得(項目))
            .ToArray();
        var 成功ジョブ数 = 選択側一覧.Count(static 側 => 側.状態==UI実行状態.完了);
        var 失敗ジョブ数 = 選択側一覧.Count(static 側 => 側.状態==UI実行状態.失敗);
        var 終端ジョブ数 = 成功ジョブ数+失敗ジョブ数;
        var 選択値ジョブ一覧 = 選択側一覧.Length;
        var 実行プロファイル = Enum.TryParse<ベンチマーク実行プロファイル>(実行.実行プロファイル,true,out var 解析済みプロファイル)
            ? 解析済みプロファイル
            : this.選択実行プロファイル.プロファイル;
        var ラベル = 実行.更新日時.ToLocalTime().ToString("yyyy-MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture)
            +" "
            +終端ジョブ数.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"/"
            +選択値ジョブ一覧.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" (成功 "+成功ジョブ数.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +",失敗 "+失敗ジョブ数.ToString(System.Globalization.CultureInfo.InvariantCulture)+")";
        return new ベンチマーク実行選択肢(
            unchecked((int)Math.Min(実行.実行ID,int.MaxValue)),
            実行.実行ID,
            ラベル,
            string.Empty,
            実行プロファイル,
            実行.更新日時,
            実行.更新日時,
            終端ジョブ数,
            選択値ジョブ一覧,
            要素件数一覧);
    }

    /// <summary>
    /// 選択プロファイルの実行一覧を更新Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 選択プロファイルの実行一覧を更新Async() {
        try {
            await this.DB実行一覧を更新Async(CancellationToken.None,true,新規選択を強制するか: true);
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマーク実行一覧の更新に失敗しました。",例外);
        }
    }

    /// <summary>
    /// 実行履歴をクリアAsync メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private async Task 実行履歴をクリアAsync() {
        if(!this.実行履歴をクリア可能か) {
            return;
        }

        this.更新中か=true;
        try {
            this.永続化バッチ処理.即時反映();
            await this.UI実行リポジトリ.保存完了を待機Async();
            await Task.Run(this.UI実行ストア.全記録を削除);
            this.選択実行読込要求を無効化();
            this.選択実行状態キャッシュ=null;
            this.現在実行セッション.クリア();
            this.実行選択肢一覧.Clear();
            var 新規実行選択肢 = 新規実行選択肢を作成(this.選択実行プロファイル.プロファイル);
            this.実行選択肢一覧.Add(新規実行選択肢);
            this.選択実行選択肢を設定(新規実行選択肢,false,false);
            this.新規実行用に全現在実行結果をクリア();
            this.状態文字列="履歴を消去しました";
            起動診断.書き込む("ベンチマーク実行履歴をクリアしました。");
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマーク実行履歴のクリアに失敗しました。",例外);
            this.状態文字列="履歴消去失敗 "+例外.Message;
        } finally {
            this.更新中か=false;
            this.ツールバーコマンド状態を更新();
        }
    }

    /// <summary>
    /// 実行選択肢を選択Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    private async Task 実行選択肢を選択Async(ベンチマーク実行選択肢? 選択肢) {
        var 前回 = this.選択実行選択肢;
        if(!this.実行選択肢を選択可能か(選択肢)) {
            this.選択実行選択肢を設定(前回,false,false);
            this.OnPropertyChanged(nameof(this.選択実行選択肢));
            return;
        }

        this.未保存ベンチマーク結果を破棄();

        this.選択実行選択肢を設定(選択肢,true,true);
    }

    /// <summary>
    /// 実行選択肢を選択可能か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool 実行選択肢を選択可能か(ベンチマーク実行選択肢? 選択肢) {
        if(新規実行選択肢か(選択肢)||!this.表示結果があるか) {
            return true;
        }

        return 保存DB実行選択肢か(選択肢);
    }

    /// <summary>
    /// 選択実行選択肢を設定 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <param name="読み込むか">読み込むか を指定して実行条件を制御します。</param>
    /// <param name="UI状態を保存するか">UI状態を保存するか を指定して実行条件を制御します。</param>
    private void 選択実行選択肢を設定(ベンチマーク実行選択肢? 選択肢,bool 読み込むか,bool UI状態を保存するか) {
        this.実行選択肢選択適用中か=true;
        try {
            if(!ReferenceEquals(this.選択実行選択肢値,選択肢)) {
                this.選択実行選択肢値=選択肢;
                this.OnPropertyChanged(nameof(this.選択実行選択肢));
                // 実行選択が変わったらスナップショットのメモリバッファを破棄し、次回読み出しでDBから取り直す。
                this.選択実行状態キャッシュ=null;
                this.選択実行選択肢を適用(選択肢,読み込むか);
                if(UI状態を保存するか) {
                    this.UI状態を保存();
                }
            }
        } finally {
            this.実行選択肢選択適用中か=false;
        }
    }

    /// <summary>
    /// 一時実行選択肢を削除 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 一時実行選択肢を削除() {
        for(var インデックス = this.実行選択肢一覧.Count-1;インデックス>=0;インデックス--) {
            if(this.実行選択肢一覧[インデックス].実行番号<0) {
                this.実行選択肢一覧.RemoveAt(インデックス);
            }
        }
    }

    /// <summary>
    /// 新規実行用の新規実行選択肢を選択 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 新規実行用の新規実行選択肢を選択(bool UI状態を保存するか = true) {
        var 新規実行選択肢 = this.実行選択肢一覧.FirstOrDefault(新規実行選択肢か);
        if(新規実行選択肢 is null) {
            新規実行選択肢=新規実行選択肢を作成(this.選択実行プロファイル.プロファイル);
            this.実行選択肢一覧.Insert(0,新規実行選択肢);
        }
        this.表示を維持して選択実行選択肢を設定(新規実行選択肢,UI状態を保存するか);
        this.ピッカー用選択実行選択肢変更を通知();
        this.ツールバーコマンド状態を更新();
    }

    private ベンチマーク実行選択肢? 選択実行選択肢参照を正規化() {
        var 選択値 = this.選択実行選択肢値;
        if(選択値 is null) {
            return null;
        }

        var 一致選択肢 = this.実行選択肢一覧.ToArray().FirstOrDefault(選択肢 => 選択肢 is not null
            &&選択肢.UI実行ID==選択値.UI実行ID
            &&選択肢.実行番号==選択値.実行番号);
        if(一致選択肢 is not null&&!ReferenceEquals(一致選択肢,選択値)) {
            this.選択実行選択肢値=一致選択肢;
        }

        return this.選択実行選択肢値;
    }

    private void ピッカー用選択実行選択肢変更を通知() {
        this.OnPropertyChanged(nameof(this.選択実行選択肢));
        this.UIディスパッチャー.呼び出し開始(() => this.OnPropertyChanged(nameof(this.選択実行選択肢)));
    }

    /// <summary>
    /// 選択実行選択肢を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <param name="読み込むか">読み込むか を指定して実行条件を制御します。</param>
    private void 選択実行選択肢を適用(ベンチマーク実行選択肢? 選択肢,bool 読み込むか) {
        if(新規実行選択肢か(選択肢)) {
            if(!this.ベンチマーク実行有効か&&this.現在実行セッション.結果ありか) {
                this.現在実行セッション.クリア();
            }
            if(this.選択タブの現在実行表示を復元()) {
                return;
            }
            this.選択実行表示をクリア();
            return;
        }

        this.結果要素数一覧を再構築(選択肢?.要素数一覧?? []);
        if(読み込むか&&!一時実行選択肢か(選択肢)) {
            if(選択肢?.UI実行ID>0) {
                this.選択実行をDBから読み込む(選択肢);
            } else {
                this.選択実行をDBから読み込む();
            }
        }
    }

    private void 保存DB実行から新規実行用カタログ行を復元() {
        var 表示済み選択一覧 = this.表示ベンチマーク行選択を取得();
        this.新規実行用の新規実行選択肢を選択(false);
        this.新規実行用に全現在実行結果をクリア();
        this.カタログからベンチマーク行を再構築();
        this.表示ベンチマーク行選択を適用(表示済み選択一覧);
    }

    private void 表示を維持して選択実行選択肢を設定(ベンチマーク実行選択肢? 選択肢,bool UI状態を保存するか) {
        this.実行選択肢選択適用中か=true;
        var 変更ありか = false;
        try {
            if(!ReferenceEquals(this.選択実行選択肢値,選択肢)) {
                this.選択実行選択肢値=選択肢;
                this.OnPropertyChanged(nameof(this.選択実行選択肢));
                変更ありか=true;
                this.選択実行状態キャッシュ=null;
                if(UI状態を保存するか) {
                    this.UI状態を保存();
                }
            }
        } finally {
            this.実行選択肢選択適用中か=false;
            if(変更ありか) {
                this.ベンチマーク選択集計を更新();
                this.ツールバーコマンド状態を更新();
            }
        }
    }

    private int 選択実行読込要求を無効化() {
        var 要求トークン = ++this.選択実行読込要求トークン;
        this.実行履歴読込中か=false;
        return 要求トークン;
    }

    private int 実行履歴読込を開始() {
        var 要求トークン = ++this.選択実行読込要求トークン;
        this.実行履歴読込中か=true;
        return 要求トークン;
    }

    private void 実行履歴読込を完了(int 要求トークン) {
        if(要求トークン==this.選択実行読込要求トークン) {
            this.実行履歴読込中か=false;
        }
    }

    /// <summary>
    /// 保存済み実行の読込中に一部の行がスキップされたときに発生するイベントです。
    /// イベント引数はユーザーへ表示するための詳細メッセージです。
    /// </summary>
    internal event EventHandler<string>? 読込警告が発生した;

    /// <summary>
    /// 選択実行をDBから読み込む メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="実行">実行 を指定して実行条件を制御します。</param>
    private void 選択実行をDBから読み込む(ベンチマーク実行選択肢 実行) {
        var 要求トークン = this.実行履歴読込を開始();
        this.選択実行表示をクリア();
        Task.Run(() => {
            var スキップ理由一覧 = new List<string>();
            try {
                this.UI実行リポジトリ.保存完了を待機Async().GetAwaiter().GetResult();
                var 項目一覧 = this.UI実行リポジトリ.ベンチマークスナップショット項目一覧を読み込む(実行.UI実行ID,スキップ理由一覧.Add);
                return (項目一覧: (IReadOnlyList<ベンチマークグリッドスナップショット項目>?)項目一覧,エラー: (Exception?)null,スキップ理由一覧: (IReadOnlyList<string>)スキップ理由一覧);
            } catch(Exception 例外) {
                return (項目一覧: (IReadOnlyList<ベンチマークグリッドスナップショット項目>?)null,エラー: 例外,スキップ理由一覧: (IReadOnlyList<string>)スキップ理由一覧);
            }
        }).ContinueWith(タスク => {
            this.UIディスパッチャー.呼び出し開始(() => {
                if(要求トークン!=this.選択実行読込要求トークン) {
                    return; // より新しい実行／タブ選択で上書きされたため破棄する。
                }
                try {
                    if(タスク.Result.エラー is { } 例外||タスク.Result.項目一覧 is not { } 結果一覧) {
                        起動診断.書き込む("ベンチマーク実行DBの読み込みに失敗しました。",タスク.Result.エラー);
                        this.状態文字列="DB読み込み失敗: "+(タスク.Result.エラー?.Message??"不明");
                        return;
                    }
                    this.読込ベンチマークスナップショット項目を適用(結果一覧);
                    this.進行状態からベンチマーク選択を適用(this.選択実行状態を読み込む());
                    if(タスク.Result.スキップ理由一覧.Count>0) {
                        var メッセージ = タスク.Result.スキップ理由一覧.Count+"件の行を読み込めませんでした:\n"
                            +string.Join("\n",タスク.Result.スキップ理由一覧);
                        this.状態文字列 = タスク.Result.スキップ理由一覧.Count+"件の行を読み込めませんでした";
                        this.読込警告が発生した?.Invoke(this,メッセージ);
                    }
                        } finally {
                    this.実行履歴読込を完了(要求トークン);
                }
            });
        },TaskScheduler.Default);
    }

    /// <summary>
    /// 選択実行をDBから読み込む メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択実行をDBから読み込む() {
        if(this.選択実行DB読込中か) {
            return;
        }

        this.選択実行DB読込中か=true;
        try {
            using(this.進行更新バッチを開始())
            using(this.データグリッド更新バッチを開始()) {
                var 実行 = this.選択実行選択肢;
                var 保存DB実行か = 保存DB実行選択肢か(実行);
                if(!保存DB実行か&&this.選択タブの現在実行表示を復元()) {
                    return;
                }

                if(!保存DB実行か&&this.今回実行メモリ表示を維持するか()) {
                    return;
                }

                if(実行 is null||新規実行選択肢か(実行)) {
                    this.選択実行表示をクリア();
                    return;
                }

                if(一時実行選択肢か(実行)||実行.UI実行ID<=0) {
                    this.選択実行表示をクリア();
                    return;
                }

                var 選択値実行状態 = this.選択実行状態を読み込む();
                this.進行状態からベンチマーク選択を適用(選択値実行状態);
                var 選択値要素数 = this.選択結果要素数?.値
                    ??最新終了要素数を検索(選択値実行状態,this.選択ソース定義)
                    ??実行.要素数一覧.LastOrDefault();
                if(選択値要素数<=0) {
                    this.選択実行表示をクリア();
                    return;
                }

                this.再読込せず結果要素数を選択(選択値要素数);
                this.ベンチマーク結果表示をクリア();
                this.比較行一覧値= [];
                this.結果項目一覧.Clear();
                this.表示結果項目一覧.Clear();

                this.比較行を表へ適用();

                this.選択実行状態ジョブを適用(選択値実行状態,選択値要素数);
                this.結果確認を更新(this.選択ソース定義.タブ);
                this.スキップセルを適用(選択値要素数);
                this.表示結果を更新();
                this.現在ソートを適用();
                this.改善プロンプトを更新();
                    }
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマーク実行DBの読み込みに失敗しました。",例外);
            this.状態文字列="DB読み込み失敗: "+例外.Message;
        } finally {
            this.選択実行DB読込中か=false;
            this.ツールバーコマンド状態を更新();
        }
    }

    /// <summary>
    /// 再読込せず結果要素数を選択 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    private void 再読込せず結果要素数を選択(int 要素数) {
        var 選択肢 = this.結果要素数選択肢一覧.FirstOrDefault(項目 => 項目.値==要素数);
        if(選択肢 is null) {
            選択肢=new 結果要素数選択肢(要素数,this.結果要素数表示名を整形(要素数));
            var 並び替え済み = this.結果要素数選択肢一覧
                .Append(選択肢)
                .OrderBy(static 項目 => 項目.値)
                .ToArray();
            this.結果要素数選択肢一覧.Clear();
            foreach(var 項目 in 並び替え済み) {
                this.結果要素数選択肢一覧.Add(項目);
            }
        }

        this.結果要素数適用中か=true;
        try {
            this.選択結果要素数=選択肢;
        } finally {
            this.結果要素数適用中か=false;
        }
    }

    /// <summary>
    /// 新規実行選択肢を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行プロファイル">実行プロファイル を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static ベンチマーク実行選択肢 新規実行選択肢を作成(ベンチマーク実行プロファイル 実行プロファイル) =>
        新規実行選択肢 with {
            実行プロファイル=実行プロファイル,
        };

    /// <summary>
    /// 新規実行選択肢か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 新規実行選択肢か(ベンチマーク実行選択肢? 選択肢) =>
        選択肢 is not null&&選択肢.実行番号==新規実行番号;

    /// <summary>
    /// 未保存実行選択肢か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 未保存実行選択肢か(ベンチマーク実行選択肢? 選択肢) =>
        選択肢 is not null&&選択肢.実行番号==未保存実行番号;

    /// <summary>
    /// 外部実行選択肢か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 外部実行選択肢か(ベンチマーク実行選択肢? 選択肢) =>
        選択肢 is not null&&選択肢.実行番号==外部実行番号;

    /// <summary>
    /// 一時実行選択肢か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 一時実行選択肢か(ベンチマーク実行選択肢? 選択肢) =>
        未保存実行選択肢か(選択肢)||外部実行選択肢か(選択肢);

    private static bool 保存DB実行選択肢か(ベンチマーク実行選択肢? 選択肢) =>
        選択肢 is not null&&!新規実行選択肢か(選択肢)&&!一時実行選択肢か(選択肢)&&選択肢.UI実行ID>0;

    /// <summary>
    /// 表示用に選択実行をDBから読み込むべきか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool 表示用に選択実行をDBから読み込むべきか() =>
        ベンチマーク実行表示元ロジック.選択実行をDBから読み込むか(
            this.選択タブに現在実行結果があるか,
            this.現在実行セッション.結果ありか,
            this.ベンチマーク実行有効か,
            this.選択実行DB読込中か,
            外部実行選択肢か(this.選択実行選択肢),
            新規実行選択肢か(this.選択実行選択肢),
            一時実行選択肢か(this.選択実行選択肢),
            保存DB実行選択肢か(this.選択実行選択肢));

    private 進行状態? DB実行から進行状態を作成(long 実行ID,UI実行記録 実行) {
        var 設定 = this.UI実行ストア.実行設定を取得(実行ID);
        if(設定 is null||string.IsNullOrWhiteSpace(設定.実行ルート)) {
            return null;
        }

        var 実行プロファイル = Enum.TryParse<ベンチマーク実行プロファイル>(実行.実行プロファイル,true,out var 解析済みプロファイル)
            ? 解析済みプロファイル
            : this.選択実行プロファイル.プロファイル;
        var 状態マネージャー = new ベンチマーク状態マネージャー(this.ベンチマークルートを取得(実行プロファイル));
        return 状態マネージャー.読み込む(設定.実行ルート);
    }
}
