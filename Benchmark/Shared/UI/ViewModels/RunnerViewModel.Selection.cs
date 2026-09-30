using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// 選択とチェック状態
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// 全ベンチマーク選択状態値 フィールドです。
    /// </summary>
    private bool? 全ベンチマーク選択状態値;

    /// <summary>
    /// 選択ベンチマークセルありか値 フィールドです。
    /// </summary>
    private bool 内部選択ベンチマークセルありか;

    /// <summary>
    /// 選択ソースタブ値 フィールドです。
    /// </summary>
    private ソースタブ選択肢 選択ソースタブ値;

    /// <summary>
    /// 選択実行プロファイル値 フィールドです。
    /// </summary>
    private 実行プロファイル選択肢 選択実行プロファイル値;

    /// <summary>
    /// 選択閾値値 フィールドです。
    /// </summary>
    private 閾値選択肢 選択閾値値;

    /// <summary>
    /// 選択結果要素数値 フィールドです。
    /// </summary>
    private 結果要素数選択肢? 選択結果要素数値;

    /// <summary>
    /// ベンチマーク選択適用中か フィールドです。
    /// </summary>
    private bool ベンチマーク選択適用中か;

    /// <summary>
    /// ソースタブ選択適用中か フィールドです。
    /// </summary>
    private bool ソースタブ選択適用中か;

    /// <summary>
    /// ベンチマーク選択表示更新中か フィールドです。
    /// </summary>
    private bool ベンチマーク選択表示更新中か;

    /// <summary>
    /// 保留ソースタブ選択肢 フィールドです。
    /// </summary>
    private ソースタブ選択肢? 保留ソースタブ選択肢;

    // 選択中の保存済み実行の進行状態スナップショット。
    // タブ切替時にDBを再読み込みしないため、実行選択の変更と実行終了時に破棄する。
    private (long UI実行ID,進行状態? 状態)? 選択実行状態キャッシュ;

    /// <summary>
    /// 実行プロファイル一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<実行プロファイル選択肢> 実行プロファイル一覧を作成() => [
            new("高精度",ベンチマーク実行プロファイル.正確),
            new("バランス",ベンチマーク実行プロファイル.中間),
            new("高速",ベンチマーク実行プロファイル.高速),
#if DEBUG
            new("テスト",ベンチマーク実行プロファイル.テスト),
#endif
        ];

    /// <summary>
    /// ベンチマーク列選択グループ一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク列選択グループ> ベンチマーク列選択グループ一覧を作成() => [];

    /// <summary>
    /// ベンチマークプロジェクト選択グループ一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク列選択グループ> ベンチマークプロジェクト選択グループ一覧を作成() => [
        new("左",this.選択ソース定義.左見出し,["左"]),
        new("右",this.選択ソース定義.右見出し,["右"]),
    ];

    /// <summary>
    /// 選択ソースタブ表示状態 プロパティです。
    /// </summary>
    /// <value>選択ソースタブ表示状態 の値を取得/設定します。</value>
    public ベンチマークソースタブ表示状態 選択ソースタブ表示状態 => this.ソースタブ表示状態を取得(this.選択ソース定義.タブ);

    /// <summary>
    /// 結果要素数選択肢一覧 プロパティです。
    /// </summary>
    /// <value>結果要素数選択肢一覧 の値を取得/設定します。</value>
    public ObservableCollection<結果要素数選択肢> 結果要素数選択肢一覧 { get; } = [];

    /// <summary>
    /// ベンチマーク列選択グループ一覧 プロパティです。
    /// </summary>
    /// <value>ベンチマーク列選択グループ一覧 の値を取得/設定します。</value>
    public IReadOnlyList<ベンチマーク列選択グループ> ベンチマーク列選択グループ一覧 { get; }
    /// <summary>
    /// ベンチマークプロジェクト選択グループ一覧 プロパティです。
    /// </summary>
    /// <value>ベンチマークプロジェクト選択グループ一覧 の値を取得/設定します。</value>
    public IReadOnlyList<ベンチマーク列選択グループ> ベンチマークプロジェクト選択グループ一覧 { get; }
    /// <summary>
    /// ソースタブ一覧 プロパティです。
    /// </summary>
    /// <value>ソースタブ一覧 の値を取得/設定します。</value>
    public IReadOnlyList<ソースタブ選択肢> ソースタブ一覧 { get; }
    /// <summary>
    /// 全体ソースタブ進行 プロパティです。
    /// </summary>
    /// <value>全体ソースタブ進行 の値を取得/設定します。</value>
    public ベンチマークタブ進行 全体ソースタブ進行 {
        get => this.全体ソースタブ進行値;
        private set {
            if(this.SetProperty(ref this.全体ソースタブ進行値,value)) {
                this.OnPropertyChanged(nameof(this.全体ソース進行概要文字列));
                this.実行予測を更新();
            }
        }
    }

    /// <summary>
    /// 実行プロファイル一覧 プロパティです。
    /// </summary>
    /// <value>実行プロファイル一覧 の値を取得/設定します。</value>
    public IReadOnlyList<実行プロファイル選択肢> 実行プロファイル一覧 { get; }
    /// <summary>
    /// 閾値選択肢一覧 プロパティです。
    /// </summary>
    /// <value>閾値選択肢一覧 の値を取得/設定します。</value>
    public IReadOnlyList<閾値選択肢> 閾値選択肢一覧 { get; }
    /// <summary>
    /// 全選択コマンド プロパティです。
    /// </summary>
    /// <value>全選択コマンド の値を取得/設定します。</value>
    public IRelayCommand 全選択コマンド { get; }
    /// <summary>
    /// 選択クリアコマンド プロパティです。
    /// </summary>
    /// <value>選択クリアコマンド の値を取得/設定します。</value>
    public IRelayCommand 選択クリアコマンド { get; }
    /// <summary>
    /// 実行コマンド プロパティです。
    /// </summary>
    /// <value>実行コマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand 実行コマンド { get; }
    /// <summary>
    /// DB保存コマンド プロパティです。
    /// </summary>
    /// <value>DB保存コマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand DB保存コマンド { get; }
    /// <summary>
    /// 新規グリッドコマンド プロパティです。
    /// </summary>
    /// <value>新規グリッドコマンド の値を取得/設定します。</value>
    public IRelayCommand 新規グリッドコマンド { get; }
#if DEBUG
    /// <summary>
    /// 無限実行コマンド プロパティです。
    /// </summary>
    /// <value>無限実行コマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand 無限実行コマンド { get; }
#endif
    /// <summary>
    /// 実行履歴クリアコマンド プロパティです。
    /// </summary>
    /// <value>実行履歴クリアコマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand 実行履歴クリアコマンド { get; }
    /// <summary>
    /// 実行切替コマンド プロパティです。
    /// </summary>
    /// <value>実行切替コマンド の値を取得/設定します。</value>
    public IRelayCommand 実行切替コマンド { get; }
    /// <summary>
    /// 停止コマンド プロパティです。
    /// </summary>
    /// <value>停止コマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand 停止コマンド { get; }
    /// <summary>
    /// 一時停止コマンド プロパティです。
    /// </summary>
    /// <value>一時停止コマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand 一時停止コマンド { get; }
    /// <summary>
    /// キャンセルコマンド プロパティです。
    /// </summary>
    /// <value>キャンセルコマンド の値を取得/設定します。</value>
    public IRelayCommand キャンセルコマンド { get; }
    /// <summary>
    /// 改善プロンプトコピーコマンド プロパティです。
    /// </summary>
    /// <value>改善プロンプトコピーコマンド の値を取得/設定します。</value>
    public IAsyncRelayCommand 改善プロンプトコピーコマンド { get; }
    public IAsyncRelayCommand 速度改善プロンプトコピーコマンド { get; }
    public IAsyncRelayCommand メモリ改善プロンプトコピーコマンド { get; }
    public IAsyncRelayCommand ログコピーコマンド { get; }
    /// <summary>
    /// 状態文字列 プロパティです。
    /// </summary>
    /// <value>状態文字列 の値を取得/設定します。</value>
    public string 状態文字列 {
        get => this.状態文字列値;
        set => this.SetProperty(ref this.状態文字列値,value);
    }

    /// <summary>
    /// ベンチマーク選択を変更できるか プロパティです。
    /// </summary>
    /// <value>ベンチマーク選択を変更できるか の値を取得/設定します。</value>
    public bool ベンチマーク選択を変更できるか =>
        !this.更新中か;

    /// <summary>
    /// 選択ベンチマークセルありか プロパティです。
    /// </summary>
    /// <value>選択ベンチマークセルありか の値を取得/設定します。</value>
    public bool 選択ベンチマークセルありか {
        get => this.内部選択ベンチマークセルありか;
        private set {
            if(this.SetProperty(ref this.内部選択ベンチマークセルありか,value)) {
                this.実行コマンド.NotifyCanExecuteChanged();
#if DEBUG
                this.無限実行コマンド.NotifyCanExecuteChanged();
#endif
                this.実行切替コマンド.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
                this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
                this.OnPropertyChanged(nameof(this.実行ボタンツールチップ文字列));
                this.OnPropertyChanged(nameof(this.ツールバー状態入力));
            }
        }
    }

    /// <summary>
    /// 全ベンチマーク選択状態 プロパティです。
    /// </summary>
    /// <value>全ベンチマーク選択状態 の値を取得/設定します。</value>
    public bool? 全ベンチマーク選択状態 {
        get => this.全ベンチマーク選択状態値;
        private set => this.SetProperty(ref this.全ベンチマーク選択状態値,value);
    }

    /// <summary>
    /// 選択下段タブ プロパティです。
    /// </summary>
    /// <value>選択下段タブ の値を取得/設定します。</value>
    public ベンチマーク下段タブ 選択下段タブ {
        get => this.選択下段タブ値;
        set {
            if(this.SetProperty(ref this.選択下段タブ値,value)) {
                this.OnPropertyChanged(nameof(this.ログ下段タブ表示中か));
            }
        }
    }

    /// <summary>
    /// ログ下段タブ表示中か プロパティです。
    /// </summary>
    /// <value>ログ下段タブ表示中か の値を取得/設定します。</value>
    public bool ログ下段タブ表示中か => this.選択下段タブ==ベンチマーク下段タブ.ログ;

    /// <summary>
    /// 選択比較ペア プロパティです。
    /// </summary>
    /// <value>選択比較ペア の値を取得/設定します。</value>
    public 比較ペア選択肢 選択比較ペア => this.選択ソース定義.ペア;

    /// <summary>
    /// 選択ソースタブ プロパティです。
    /// </summary>
    /// <value>選択ソースタブ の値を取得/設定します。</value>
    public ソースタブ選択肢 選択ソースタブ {
        get => this.選択ソースタブ値;
        set {
            if(value==this.選択ソースタブ値) {
                return;
            }

            if(this.更新中か) {
                // 読み込み中の切替は破棄せず保留し、読み込み完了後に最後の要求だけを適用する。
                this.保留ソースタブ選択肢=value;
                return;
            }

            if(this.ソースタブ選択適用中か) {
                this.ソースタブ選択を適用(value);
                return;
            }

            _=this.ソースタブを選択Async(value);
        }
    }

    /// <summary>
    /// 選択ソース定義 プロパティです。
    /// </summary>
    /// <value>選択ソース定義 の値を取得/設定します。</value>
    public ベンチマークソースタブ定義 選択ソース定義 => this.選択ソースタブ.定義;

    /// <summary>
    /// ソースタブ選択を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    private void ソースタブ選択を適用(ソースタブ選択肢 選択肢) {
        var 前回タブ = this.選択ソースタブ値.定義.ID;
        var 次タブ = 選択肢.定義.ID;
        var ストップウォッチ = System.Diagnostics.Stopwatch.StartNew();
        var 変更ありか = false;
        起動診断.パンくずを追加(
            "ソースタブ切替開始 "
            +前回タブ+"->"+次タブ
            +" 実行中="+this.ベンチマーク実行有効か.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" 有効="+(this.有効実行ソースタブ?.ToString()??"-")
            +" 進行="+this.全体ソースタブ進行.割合文字列);
        this.ソースタブ選択適用中か=true;
        this.選択実行読込要求を無効化();
        try {
            using(this.進行更新バッチを開始()) {
                if(this.SetProperty(ref this.選択ソースタブ値,選択肢)) {
                    変更ありか=true;
                    this.ソースタブ表示選択を適用();
                    this.OnPropertyChanged(nameof(this.選択ソースタブ表示状態));
                    this.OnPropertyChanged(nameof(this.ベンチマーク行一覧));
                    this.OnPropertyChanged(nameof(this.ベンチマークシェア概要文));
                    this.OnPropertyChanged(nameof(this.選択比較ペア));
                    this.OnPropertyChanged(nameof(this.選択ソース定義));
                    this.OnPropertyChanged(nameof(this.ソート規則編集可能か));
                    this.OnPropertyChanged(nameof(this.ベンチマーク選択を変更できるか));
                    this.ベンチマークプロジェクト見出し文字列を更新();
                    // タブ切替では DB もカタログも再読み込みせず、メモリ上のカタログと結果バッファだけで再構築する。
                    this.ベンチマークソート列選択肢を更新();
                    // ヘッダーのまとめチェック(FLinq/ZLinq)は現在のSourceタブだけを反映する共有インスタンスのため、
                    // 切替直後に再計算しないと直前のタブのチェック状態が見えたままになる。
                    this.ベンチマーク選択集計を更新();
                }
            }
        } catch(Exception 例外) {
            起動診断.書き込む(
                "ソースタブ切替失敗 "
                +前回タブ+"->"+次タブ
                +" 実行中="+this.ベンチマーク実行有効か.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 有効="+(this.有効実行ソースタブ?.ToString()??"-"),
                例外);
            起動診断.クラッシュ状況を書き込む("ソースタブ切替失敗");
            throw;
        } finally {
            ストップウォッチ.Stop();
            if(変更ありか) {
                起動診断.書き込む(
                    $"ソースタブ切替 {前回タブ}->{次タブ} 経過={切替計測ミリ秒を整形(ストップウォッチ.Elapsed)}ms 行数={this.ベンチマーク行一覧.Count}");
            }
            起動診断.パンくずを追加(
                "ソースタブ切替終了 "
                +前回タブ+"->"+次タブ
                +" 変更あり="+変更ありか.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 経過="+切替計測ミリ秒を整形(ストップウォッチ.Elapsed)
                +" 行数="+this.ベンチマーク行一覧.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 進行="+this.全体ソースタブ進行.割合文字列);
            this.ソースタブ選択適用中か=false;
        }
    }

    /// <summary>
    /// ソースタブ表示選択を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ソースタブ表示選択を適用() {
        foreach(var 項目 in this.ソースタブ一覧) {
            項目.選択状態を設定(ReferenceEquals(項目,this.選択ソースタブ値));
        }
    }

    /// <summary>
    /// 選択実行プロファイル プロパティです。
    /// </summary>
    /// <value>選択実行プロファイル の値を取得/設定します。</value>
    public 実行プロファイル選択肢 選択実行プロファイル {
        get => this.選択実行プロファイル値;
        set {
            if(!this.ベンチマーク条件を変更可能か) {
                return;
            }

            if(this.SetProperty(ref this.選択実行プロファイル値,value)) {
                this.新規実行用の新規実行選択肢を選択(false);
                this.プロファイル実行一覧更新=Task.WhenAll(this.プロファイル実行一覧更新,this.選択プロファイルの実行一覧を更新Async());
            }
        }
    }

    /// <summary>
    /// 選択閾値 プロパティです。
    /// </summary>
    /// <value>選択閾値 の値を取得/設定します。</value>
    public 閾値選択肢 選択閾値 {
        get => this.選択閾値値;
        set {
            if(!this.ベンチマーク条件を変更可能か) {
                return;
            }

            this.SetProperty(ref this.選択閾値値,value);
        }
    }

    /// <summary>
    /// 選択結果要素数 プロパティです。
    /// </summary>
    /// <value>選択結果要素数 の値を取得/設定します。</value>
    public 結果要素数選択肢? 選択結果要素数 {
        get => this.選択結果要素数値;
        set {
            if(this.SetProperty(ref this.選択結果要素数値,value)) {
                if(value is not null) {
                    this.現在要素数文字列=要素数文字列を整形(value.値);
                }
                if(!this.結果要素数適用中か) {
                    this.表示結果を更新();
                    this.選択要素数を表へ適用();
                }
                if(!this.UI状態適用中か&&!this.結果要素数適用中か&&!this.ベンチマーク実行中か) {
                    this.UI状態を保存();
                }
            }
        }
    }

    private ソースタブ選択肢 ソースタブ選択肢を取得(ベンチマークソースタブ ソースタブ) =>
        this.ソースタブ一覧.FirstOrDefault(選択肢 => 選択肢.定義.タブ==ソースタブ)??this.ソースタブ一覧[0];

    /// <summary>
    /// ソースタブ プロパティです。
    /// </summary>
    /// <value>ソースタブ の値を取得/設定します。</value>
    public void 全ベンチマーク選択を切り替える(ベンチマークソースタブ定義? ソースタブ = null) {
        if(!this.ベンチマーク選択を変更できるか) {
            return;
        }

        var 行一覧 = ソースタブ is { } タブ
            ? this.ベンチマーク行一覧を取得(タブ).Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか)).ToArray()
            : this.全ベンチマーク行を取得().Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか)).ToArray();
        var 状態 = 行選択状態を取得(行一覧);
        if(状態 is null) {
            this.ベンチマーク行一覧を設定(行一覧,true);
            return;
        }

        if(状態 is true) {
            this.ベンチマーク行一覧を設定(行一覧,false);
            return;
        }

        this.ベンチマーク行一覧を設定(行一覧,true);
    }

    /// <summary>
    /// 保留ソースタブ選択を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 保留ソースタブ選択を適用() {
        if(this.保留ソースタブ選択肢 is { } 保留) {
            this.保留ソースタブ選択肢=null;
            if(保留!=this.選択ソースタブ値) {
                _=this.ソースタブを選択Async(保留);
            }
        }
    }

    /// <summary>
    /// ソースタブ プロパティです。
    /// </summary>
    /// <value>ソースタブ の値を取得/設定します。</value>
    private void 全ベンチマーク選択を設定(bool 選択中か,ベンチマークソースタブ定義? ソースタブ = null) {
        if(!this.ベンチマーク選択を変更できるか) {
            return;
        }

        var 行一覧 = ソースタブ is { } タブ
            ? this.ベンチマーク行一覧を取得(タブ).Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか))
            : this.全ベンチマーク行を取得().Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか));
        this.ベンチマーク行一覧を設定(行一覧,選択中か);
    }

    // 列/プロジェクトヘッダーは現在タブだけを反映する。左上の全体チェックは全Sourceタブを対象にする。
    /// <summary>
    /// ベンチマーク選択集計を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマーク選択集計を更新() {
        foreach(var 状態 in this.ソースタブ表示状態一覧) {
            var 行一覧 = 状態.行一覧
                .Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか))
                .ToArray();
            状態.ベンチマーク選択状態=行選択状態を取得(行一覧);
        }
        var 全行一覧 = this.全ベンチマーク行を取得()
            .Where(static 行 => 行.プロジェクトセル一覧.Any(static セル => セル.ケースありか))
            .ToArray();
        this.全ベンチマーク選択状態=保存DB実行選択肢か(this.選択実行選択肢)&&!this.ベンチマーク実行有効か
            ? false
            : 行選択状態を取得(全行一覧);
        this.選択ベンチマークセルありか=this.選択ベンチマークセルがあるか();
        this.ソースタブ進行を更新();
    }

    // 実行ボタンの活性はライブなチェック状態で判定する。進捗バッジ用の進行セル一覧作成は
    // 実行時に凍結した選択スナップショットを優先するため、実行後に選択を変えると実態と乖離し、
    // 「ボタンは有効なのに 実行Async が選択0件で無言中断する」原因になる。
    /// <summary>
    /// 選択ベンチマークセルがあるか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool 選択ベンチマークセルがあるか() =>
        this.ソースタブ一覧.Any(選択肢 => this.選択ライブベンチマークセルがあるか(選択肢.定義));

    /// <summary>
    /// 選択ライブベンチマークセルがあるか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool 選択ライブベンチマークセルがあるか(ベンチマークソースタブ定義 ソースタブ) {
        var 状態 = this.ソースタブ表示状態を取得(ソースタブ.タブ);
        if(状態.行一覧.Count>0) {
            return 状態.行一覧.Any(static 行 => 行.選択中か&&行.プロジェクトセル一覧.Any(static セル => セル.ケースありか));
        }

        return ベンチマーク行グループ化ロジック.行を作成(this.カタログケース一覧,ソースタブ)
            .Any(行 => 行.プロジェクト別ケース.Count>0
                &&this.ベンチマーク行選択状態.TryGetValue(行選択キーを作成(ソースタブ,行.行キー),out var 選択中か)
                &&選択中か);
    }

    private void 新規選択用に復元実行再開状態をクリア() {
        this.有効UI実行ID=null;
        this.現在未保存実行ルート=null;
        this.有効実行ルートを設定(null);
    }

    private bool 未完了選択測定があるか(long 実行ID) {
        foreach(var 項目 in this.UI実行ストア.ベンチマークスナップショット項目一覧を取得(実行ID)) {
            if(!項目.選択中か) {
                continue;
            }
            if(スナップショット側一覧を取得(項目).Any(static 側 => 側.状態 is UI実行状態.待機中 or UI実行状態.実行中 or UI実行状態.一時停止中)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 実行選択を復元 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="実行番号">実行番号 を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    private void 実行選択を復元(int? 実行番号,int? 要素数) {
        this.UI状態適用中か=true;
        try {
            var 選択肢 = 実行番号 is { } 番号
                ? this.実行選択肢一覧.FirstOrDefault(選択肢 => 選択肢.実行番号==番号)??this.実行選択肢一覧.FirstOrDefault()
                : this.実行選択肢一覧.FirstOrDefault();
            if(新規実行選択肢か(選択肢)) {
                this.表示を維持して選択実行選択肢を設定(選択肢,false);
            } else {
                this.選択実行選択肢を設定(選択肢,false,false);
            }
            this.結果要素数一覧を再構築(this.選択実行選択肢?.要素数一覧?? []);
        } finally {
            this.UI状態適用中か=false;
            this.ベンチマーク選択集計を更新();
        }
        if(!新規実行選択肢か(this.選択実行選択肢)) {
            this.選択実行をDBから読み込む();
        }
    }

    /// <summary>
    /// 現在ベンチマーク選択を取得 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 現在ベンチマーク選択を取得() {
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            this.ベンチマーク行選択状態[行選択キーを作成(行.ソースタブ,行.行キー)]=行.選択中か;
        }
    }

    private void 保存ベンチマーク選択を適用(ベンチマーク表行 行) {
        var 行キー一覧 = this.ベンチマーク選択行キー一覧を取得(行);
        var 行選択値か = 行キー一覧.Any(キー => this.ベンチマーク行選択状態.GetValueOrDefault(
            行選択キーを作成(行.ソースタブ,キー),
            false));
        行.選択中か=行選択値か;
    }

    private void 選択実行状態選択キャッシュを更新() {
        var 選択値実行ID = this.選択実行選択肢?.UI実行ID??0;
        if(選択値実行ID<=0
            ||this.選択実行状態キャッシュ is not { } キャッシュ
            ||キャッシュ.UI実行ID!=選択値実行ID
            ||キャッシュ.状態 is null) {
            return;
        }

        キャッシュ.状態.ベンチマーク選択.行選択.Clear();
        foreach(var 項目 in this.ベンチマーク行選択状態) {
            キャッシュ.状態.ベンチマーク選択.行選択[項目.Key]=項目.Value;
        }
    }

    /// <summary>
    /// ベンチマーク選択表示を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマーク選択表示を更新() {
        if(this.ベンチマーク選択表示更新中か||this.ベンチマーク行一覧.Count==0) {
            return;
        }

        if(保存DB実行選択肢か(this.選択実行選択肢)&&this.結果項目一覧.Count>0&&!this.ベンチマーク実行有効か) {
            this.表示結果を更新();
            this.ソースタブ進行を更新();
            return;
        }

        this.ベンチマーク選択表示更新中か=true;
        try {
            this.表示結果を更新();
            this.表示結果から比較行を更新();
            this.比較行を表へ適用();
            this.選択表示結果を表へ適用();
            if(this.選択結果要素数 is { } 選択値要素数) {
                this.スキップセルを適用(選択値要素数.値);
            }
            this.最終完了状態を更新();
            this.ソースタブ進行を更新();
        } finally {
            this.ベンチマーク選択表示更新中か=false;
        }
    }

    /// <summary>
    /// 進行状態からベンチマーク選択を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    private void 進行状態からベンチマーク選択を適用(進行状態? 状態) {
        if(状態 is null) {
            return;
        }

        this.UI状態適用中か=true;
        try {
            this.ベンチマーク行選択状態.Clear();
            foreach(var 項目 in 状態.ベンチマーク選択.行選択) {
                this.ベンチマーク行選択状態[項目.Key]=項目.Value;
            }
            foreach(var 行 in this.全ベンチマーク行を取得()) {
                this.保存ベンチマーク選択を適用(行);
            }
        } finally {
            this.UI状態適用中か=false;
            this.ベンチマーク選択集計を更新();
            this.ベンチマーク選択表示を更新();
        }
    }

    /// <summary>
    /// 未保存実行選択を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 未保存実行選択を更新() {
        if(!this.未保存ベンチマーク結果ありか||!this.現在実行セッション.結果ありか) {
            this.ツールバーコマンド状態を更新();
            return;
        }

        this.新規実行用の新規実行選択肢を選択();
        this.ツールバーコマンド状態を更新();
    }

    /// <summary>
    /// 選択要素数一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<int> 選択要素数一覧を取得() {
        return ベンチマーク要素数ロジック
            .適応候補を作成(ベンチマーク要素数一覧.全て,ベンチマーク要素数一覧.全て[^1])
            .ToArray();
    }

    /// <summary>
    /// 閾値選択肢を作成します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<閾値選択肢> 閾値選択肢を作成() {
        var 設定 = new 閾値選択肢設定();
        閾値選択肢を構成(設定);
        return 閾値選択肢設定.値一覧
            .Select(値 => new 閾値選択肢(
                値.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "+設定.単位,
                checked(値*設定.ミリ秒倍率)))
            .ToArray();
    }

    static partial void 閾値選択肢を構成(閾値選択肢設定 設定);

    /// <summary>
    /// 閾値選択肢の表示単位と倍率を保持します。
    /// </summary>
    /// <value>閾値選択肢設定の値を取得/設定します。</value>
    private sealed class 閾値選択肢設定 {
        public static readonly IReadOnlyList<int> 値一覧 = [1,2,4,8,16,32,64,128,256,512,1024,2048,4096,8192];
        public int ミリ秒倍率 { get; set; } = 1;
        public string 単位 { get; set; } = "ms";
    }

    /// <summary>
    /// 選択タブに現在実行結果があるか プロパティです。
    /// </summary>
    /// <value>選択タブに現在実行結果があるか の値を取得/設定します。</value>
    private bool 選択タブに現在実行結果があるか =>
        this.現在実行セッション.ソースタブ結果があるか(this.選択ソース定義.タブ);

    /// <summary>
    /// 選択タブの現在実行結果項目一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク結果項目> 選択タブの現在実行結果項目一覧を取得() =>
        this.現在実行結果項目一覧を取得(this.選択ソース定義.タブ);

    /// <summary>
    /// 選択タブの現在実行表示を復元 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool 選択タブの現在実行表示を復元() {
        var 結果一覧 = this.選択タブの現在実行結果項目一覧を取得();
        if(結果一覧.Count==0) {
            return false;
        }

        var 最新要素数 = 結果一覧
            .Where(static 項目 => 項目.軸数>0)
            .OrderBy(static 項目 => 項目.開始日時??DateTimeOffset.MinValue)
            .ThenBy(static 項目 => 項目.軸数)
            .LastOrDefault()
            ?.軸数;
        this.結果項目一覧.Clear();
        this.表示結果項目一覧.Clear();
        foreach(var 項目 in 結果一覧) {
            this.結果項目一覧.Add(項目);
        }

        this.結果要素数一覧を再構築(結果一覧
            .Select(static 項目 => 項目.軸数)
            .Where(static 項目 => 項目>0)
            .Distinct()
            .Order()
            .ToArray());
        if(最新要素数 is { } 最新要素数値) {
            this.選択結果要素数=this.結果要素数選択肢一覧.FirstOrDefault(項目 => 項目.値==最新要素数値)??this.選択結果要素数;
            this.現在要素数文字列=要素数文字列を整形(最新要素数値);
        }
        this.表示結果を更新();
        this.比較行一覧値=this.選択結果要素数 is { } 選択値要素数
            ? this.要素数の現在実行比較行を構築(選択値要素数.値)
            : this.現在実行比較行を構築();
        this.比較行を表へ適用();
        this.選択要素数を表へ適用();
        this.現在実行結果を表へ適用(結果一覧);
        this.改善プロンプトを更新();
        this.ソースタブ進行を更新();
        return true;
    }

    /// <summary>
    /// 選択実行表示をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択実行表示をクリア() {
        this.実行予測をクリア();
        using(this.データグリッド更新バッチを開始()) {
            this.ソースタブ実行中状態をクリア();
            this.全ソースタブ比較表示をクリア();
            this.フォールバック用完了結果項目を記憶();
            this.結果項目一覧.Clear();
            this.表示結果項目一覧.Clear();
            this.現在要素数文字列="-";
            this.ベンチマーク結果表示をクリア();
            this.改善プロンプト文字列=string.Empty;
        this.速度改善プロンプト文字列=string.Empty;
        this.メモリ改善プロンプト文字列=string.Empty;
            this.ソースタブ進行を更新();
            this.ツールバーコマンド状態を更新();
        }
    }

    /// <summary>
    /// 未選択結果表示をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 未選択結果表示をクリア() {
        foreach(var ベンチマーク in this.全ベンチマーク行を取得()) {
            ベンチマーク.未選択結果表示をクリア();
        }
    }

    /// <summary>
    /// 現在改善選択実行IDを取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private long? 現在改善選択実行IDを取得() {
        if(this.有効UI実行ID is { } 有効) {
            return 有効;
        }

        var 選択値実行 = this.選択実行選択肢;
        return 選択値実行 is not null&&!新規実行選択肢か(選択値実行)&&!一時実行選択肢か(選択値実行)&&選択値実行.UI実行ID>0
            ? 選択値実行.UI実行ID
            : null;
    }

    /// <summary>
    /// 進行セルを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="選択中か">選択中か を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static ベンチマークタブ進行セル 進行セルを作成(ベンチマーク表行 行,string プロジェクト,bool 選択中か) {
        行.プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク);
        return new ベンチマークタブ進行セル(
            行.メソッド名,
            プロジェクト,
            ベンチマーク?.型完全名??string.Empty,
            ベンチマーク?.引数キー??string.Empty,
            選択中か,
            行.行キー);
    }

    private static void 行UI選択を設定(ベンチマーク表行 行,bool 選択中か) {
        foreach(var セル in 行.プロジェクトセル一覧) {
            セル.UI選択中か=セル.ケースありか&&選択中か;
        }
        行.UI選択状態を更新();
    }

    /// <summary>
    /// 選択実行状態ジョブを適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    private void 選択実行状態ジョブを適用(進行状態? 状態,int 要素数) {
        if(状態 is null) {
            return;
        }

        var ジョブ一覧 = 状態.ジョブ一覧
            .Where(ジョブ => this.選択ソース定義.プロジェクトを含むか(ジョブ.プロジェクト)
                &&終了ジョブか(ジョブ))
            .GroupBy(static ジョブ => ベンチマーク行グループ化ロジック.ケースキーを作成(ジョブ.プロジェクト,ジョブ.型完全名,ジョブ.メソッド名,ジョブ.引数キー),StringComparer.OrdinalIgnoreCase)
            .SelectMany(static グループ => グループ
                .GroupBy(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ))
                .Select(static 要素グループ => 要素グループ
                    .OrderBy(static ジョブ => ジョブ.終了日時??ジョブ.開始日時??DateTimeOffset.MinValue)
                    .Last()))
            .ToArray();
        foreach(var ジョブ in ジョブ一覧) {
            var 項目 = new ベンチマーク結果項目(ジョブ);
            this.結果項目を追加または更新(項目);
            if(ベンチマーク作業軸.軸数を取得(ジョブ)!=要素数) {
                continue;
            }

            var CPU = this.割当CPUを検索(項目);
            var 完了日時 = ジョブ.終了日時??ジョブ.開始日時??DateTimeOffset.Now;
            var 行 = this.ベンチマーク行を検索(ジョブ);
            行?.実行結果を適用(項目,CPU,完了日時,false);
        }
        this.ソースタブ進行を更新();
    }

    /// <summary>
    /// 選択要素数を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択要素数を表へ適用() {
        var 選択値要素数 = this.選択結果要素数?.値;
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            行.ベンチマーク名を更新(選択値要素数??0);
        }
        if(!this.ベンチマーク実行有効か&&this.結果項目一覧.Count==0&&this.表示用に選択実行をDBから読み込むべきか()) {
            this.選択実行をDBから読み込む();
            return;
        }
        this.全比較行を表示結果から更新();
        this.比較行を全表へ適用();
        if(選択値要素数 is { } スキップ済み要素数) {
            this.スキップセルを適用(スキップ済み要素数);
        }
        this.最終完了状態を更新();
        this.ベンチマーク選択集計を更新();
    }

    /// <summary>
    /// スキップセルを適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="選択値要素数">選択値要素数 を指定して実行条件を制御します。</param>
    private void スキップセルを適用(int 選択値要素数) {
        var ストップウォッチ = Stopwatch.StartNew();
        var 表示結果一覧 = this.表示用結果ソースを取得();
        var 測定一覧 = this.表示測定一覧を作成(表示結果一覧);
        var 閾値ナノ秒 = this.適応閾値を取得().TotalMilliseconds*1_000_000;
        foreach(var 行 in this.ベンチマーク行一覧) {
            foreach(var ペア in this.ベンチマークペア一覧を取得()) {
                var 左 = 行.プロジェクトセルを取得(ペア.左プロジェクト);
                var 右 = 行.プロジェクトセルを取得(ペア.右プロジェクト);
                if(左 is not { ケースありか: true }||右 is not { ケースありか: true }) {
                    continue;
                }
                if(!行.選択中か) {
                    continue;
                }

                var 左型完全名 = 行の型完全名を取得(行,ペア.左プロジェクト);
                var 左引数キー = 行の引数キーを取得(行,ペア.左プロジェクト);
                var 右型完全名 = 行の型完全名を取得(行,ペア.右プロジェクト);
                var 右引数キー = 行の引数キーを取得(行,ペア.右プロジェクト);
                var 左結果ありか = this.結果があるか(表示結果一覧,行.メソッド名,ペア.左プロジェクト,左型完全名,選択値要素数,左引数キー);
                var 右結果ありか = this.結果があるか(表示結果一覧,行.メソッド名,ペア.右プロジェクト,右型完全名,選択値要素数,右引数キー);
                if(左結果ありか&&右結果ありか) {
                    continue;
                }

                var 左進行セル = 進行セルを作成(行,ペア.左プロジェクト,行.選択中か);
                var 右進行セル = 進行セルを作成(行,ペア.右プロジェクト,行.選択中か);
                var 進行セル一覧 = new[] { 左進行セル,右進行セル };
                if(ベンチマークペア完了ロジック.選択ペアが閾値完了済みか(
                    測定一覧,
                    進行セル一覧,
                    左進行セル,
                    ペア.左プロジェクト,
                    ペア.右プロジェクト,
                    閾値ナノ秒)) {
                    continue;
                }

                var 停止済み日時 = ベンチマークスキップ表示ロジック.ペア停止要素数を検索(
                        測定一覧,
                        左進行セル,
                        右進行セル,
                        選択値要素数,
                        閾値ナノ秒)
                    ??ベンチマークスキップ表示ロジック.直前の完了ペア要素数を検索(
                        測定一覧,
                        左進行セル,
                        右進行セル,
                        選択値要素数);
                if(停止済み日時 is { } 停止済み日時要素数) {
                    行.ペア停止を適用(
                        ペア.左プロジェクト,
                        ペア.右プロジェクト,
                        停止済み日時要素数,
                        this.ベンチマーク実行有効か&&this.選択タブに現在実行結果があるか);
                    if(行.選択中か&&!左結果ありか) {
                        this.スキップセルを適用(表示結果一覧,行,ペア.左プロジェクト,停止済み日時要素数);
                    }
                    if(行.選択中か&&!右結果ありか) {
                        this.スキップセルを適用(表示結果一覧,行,ペア.右プロジェクト,停止済み日時要素数);
                    }
                }
            }
        }

        UIスレッド予算.記録("skipped-cells",ストップウォッチ.Elapsed.TotalMilliseconds);
    }

    private (int 測定済み,int 選択済み) 測定済み選択セル数を計算(ベンチマークソースタブ定義 ソースタブ) {
        var 測定済み = 0;
        var 選択値 = 0;
        foreach(var 行 in this.ソースタブ表示状態を取得(ソースタブ.タブ).行一覧) {
            if(!行.選択中か) {
                continue;
            }
            foreach(var セル in 行.プロジェクトセル一覧) {
                if(!セル.ケースありか) {
                    continue;
                }

                選択値++;
                if(セル.測定値.測定値ありか) {
                    測定済み++;
                }
            }
        }

        return (測定済み,選択値);
    }

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private int? ペア停止要素数を検索(string メソッド,string 型完全名,string プロジェクト,int 選択値要素数,string 引数キー = "") {
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
        return ベンチマークスキップ表示ロジック.ペア停止要素数を検索(
            測定一覧,
            メソッド,
            型完全名,
            引数キー,
            プロジェクト,
            選択値要素数,
            this.適応閾値を取得().TotalMilliseconds*1_000_000);
    }

    /// <summary>
    /// 結果が表示選択中か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool 結果が表示選択中か(ベンチマーク結果項目 結果) {
        return this.結果が表示選択中か(結果,this.選択ソース定義);
    }

    /// <summary>
    /// 結果が表示選択中か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool 結果が表示選択中か(ベンチマーク結果項目 結果,ベンチマークソースタブ定義 ソースタブ) {
        if(!ソースタブ.プロジェクトを含むか(結果.プロジェクト)) {
            return false;
        }

        var 行 = this.ベンチマーク行を検索(結果);
        if(行 is null) {
            return false;
        }

        return 行.プロジェクトセル一覧.Any(セル =>
            セル.ケースありか
            &&string.Equals(セル.プロジェクト,結果.プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(行.メソッド名,結果.メソッド,StringComparison.OrdinalIgnoreCase)
            &&ベンチマーク型一致ロジック.同じ型か(行の型完全名を取得(行,セル.プロジェクト),結果.型完全名)
            &&同じ引数か(行の引数キーを取得(行,セル.プロジェクト),結果.引数キー));
    }

    /// <summary>
    /// 選択実行状態を読み込む メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private 進行状態? 選択実行状態を読み込む() {
        var 実行 = this.選択実行選択肢;
        if(実行 is null||新規実行選択肢か(実行)||一時実行選択肢か(実行)||実行.UI実行ID<=0) {
            return null;
        }

        // 過去データの読み込みは実行選択時の1回だけDBから行い、以後のタブ切替などはメモリバッファを返す。
        if(this.選択実行状態キャッシュ is { } キャッシュ&&キャッシュ.UI実行ID==実行.UI実行ID) {
            return キャッシュ.状態;
        }

        try {
            var 実行記録 = this.UI実行ストア.実行一覧を取得(UI実行アプリ種別を取得()).FirstOrDefault(項目 => 項目.実行ID==実行.UI実行ID);
            var 状態 = 実行記録 is null ? null : this.DB実行から進行状態を作成(実行.UI実行ID,実行記録);
            this.選択実行状態キャッシュ=(実行.UI実行ID,状態);
            return 状態;
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマークDB進行スナップショットの読み込みに失敗しました。",例外);
            return null;
        }
    }
}
