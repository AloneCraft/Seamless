using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// 新規実行番号 フィールドです。
    /// </summary>
    private const int 新規実行番号 = 0;

    /// <summary>
    /// 未保存実行番号 フィールドです。
    /// </summary>
    private const int 未保存実行番号 = -1;

    /// <summary>
    /// 外部実行番号 フィールドです。
    /// </summary>
    private const int 外部実行番号 = -2;

    /// <summary>
    /// 実行サービス フィールドです。
    /// </summary>
    private readonly Iベンチマーク実行サービス 実行サービス;

    /// <summary>
    /// 比較サービス フィールドです。
    /// </summary>
    private readonly Iベンチマーク比較サービス 比較サービス;

    /// <summary>
    /// UI実行リポジトリ フィールドです。
    /// </summary>
    private readonly ベンチマークUI実行リポジトリ UI実行リポジトリ;

    /// <summary>
    /// UIディスパッチャー フィールドです。
    /// </summary>
    private readonly IベンチマークUIディスパッチャー UIディスパッチャー;

    /// <summary>
    /// UI設定 フィールドです。
    /// </summary>
    private readonly IベンチマークUI設定 UI設定;

    /// <summary>
    /// 実行イベントパイプライン フィールドです。
    /// </summary>
    private readonly ベンチマーク実行イベントパイプライン 実行イベントパイプライン;

    /// <summary>
    /// パイプライン診断 フィールドです。
    /// </summary>
    private readonly ベンチマークUI診断 パイプライン診断;

    /// <summary>
    /// 描画スケジューラー フィールドです。
    /// </summary>
    private readonly ベンチマークUI描画スケジューラー 描画スケジューラー;

    /// <summary>
    /// 永続化バッチ処理 フィールドです。
    /// </summary>
    private readonly ベンチマーク永続化バッチ処理 永続化バッチ処理;

    /// <summary>
    /// 実行投影サービス フィールドです。
    /// </summary>
    private readonly ベンチマーク実行投影サービス 実行投影サービス = new();

    /// <summary>
    /// ベンチマークルート フィールドです。
    /// </summary>
    private readonly string ベンチマークルート;

    /// <summary>
    /// 実行キャンセル フィールドです。
    /// </summary>
    private CancellationTokenSource? 実行キャンセル;

    /// <summary>
    /// 保留実行終了種別 フィールドです。
    /// </summary>
    private 実行終了種別 保留実行終了種別;

    /// <summary>
    /// 更新中か値 フィールドです。
    /// </summary>
    private bool 内部更新中か;

    /// <summary>
    /// 現在要素数文字列値 フィールドです。
    /// </summary>
    private string 現在要素数文字列値 = "1";

    /// <summary>
    /// UI状態適用中か フィールドです。
    /// </summary>
    private bool UI状態適用中か;

    /// <summary>
    /// 結果要素数適用中か フィールドです。
    /// </summary>
    private bool 結果要素数適用中か;

    /// <summary>
    /// 終了状態保存保留ありか値 フィールドです。
    /// </summary>
    private bool 内部終了状態保存保留ありか;

    /// <summary>
    /// 未保存ベンチマーク結果ありか値 フィールドです。
    /// </summary>
    private bool 内部未保存ベンチマーク結果ありか;

    /// <summary>
    /// 現在実行セッション フィールドです。
    /// </summary>
    private readonly ベンチマーク実行セッション 現在実行セッション = new();

    /// <summary>
    /// ログ文字列値 フィールドです。
    /// </summary>
    private string ログ文字列値 = string.Empty;

    /// <summary>
    /// ログ購読 フィールドです。
    /// </summary>
    private IDisposable? ログ購読;

    /// <summary>
    /// 終了処理中か フィールドです。
    /// </summary>
    private bool 終了処理中か;

    /// <summary>
    /// 報告済みメタデータ警告 フィールドです。
    /// </summary>
    private readonly HashSet<string> 報告済みメタデータ警告 = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// 実行結果世代 フィールドです。
    /// </summary>
    private int 実行結果世代;

    /// <summary>
    /// 最終クリーンアップ後に発火する古いジョブ単位の遅延適用を無効化するためのトークンです。
    /// </summary>
    private int 最終ライブ表示クリアトークン;

    /// <summary>
    /// 実行終了時にパイプライン未反映イベントが残っていたため、最終クリーンアップを延期しているかどうかです。
    /// </summary>
    private bool 最終ライブ表示クリア保留中か;

    /// <summary>
    /// 延期中の最終クリーンアップでパイプライン更新を破棄するかどうかです。
    /// </summary>
    private bool 最終ライブ表示クリア破棄保留中か;

    /// <summary>
    /// ベンチマーク自動スクロール連番 フィールドです。
    /// </summary>
    private int ベンチマーク自動スクロール連番;

    /// <summary>
    /// 有効UI実行ID フィールドです。
    /// </summary>
    private long? 有効UI実行ID;

    /// <summary>
    /// 未保存UI実行ID フィールドです。
    /// </summary>
    private long? 未保存UI実行ID;

    /// <summary>
    /// 実行終了種別 プロパティです。
    /// </summary>
    /// <value>実行終了種別 の値を取得/設定します。</value>
    private enum 実行終了種別 {
        なし,
        停止,
        一時停止,
    }

    /// <summary>
    /// この型のインスタンスを作成し、初期状態を構築するための処理を実行します。
    /// </summary>
    /// <param name="カタログサービス">カタログサービス を指定して実行条件を制御します。</param>
    /// <param name="CPUトポロジーサービス">CPUトポロジーサービス を指定して実行条件を制御します。</param>
    /// <param name="実行サービス">実行サービス を指定して実行条件を制御します。</param>
    /// <param name="比較サービス">比較サービス を指定して実行条件を制御します。</param>
    /// <param name="UI実行ストア">UI実行ストア を指定して実行条件を制御します。</param>
    /// <param name="ベンチマークルート">ベンチマークルート を指定して実行条件を制御します。</param>
    /// <param name="UIディスパッチャー">UIディスパッチャー を指定して実行条件を制御します。</param>
    /// <param name="UI設定">UI設定 を指定して実行条件を制御します。</param>
    /// <param name="起動診断を有効にするか">起動診断を有効にするか を指定して実行条件を制御します。</param>
    internal 実行ビューモデル(
        Iベンチマークカタログサービス カタログサービス,
        ICPUトポロジーサービス CPUトポロジーサービス,
        Iベンチマーク実行サービス 実行サービス,
        Iベンチマーク比較サービス 比較サービス,
        UI実行ストア UI実行ストア,
        string ベンチマークルート,
        IベンチマークUIディスパッチャー? UIディスパッチャー = null,
        IベンチマークUI設定? UI設定 = null,
        bool 起動診断を有効にするか = true) {
        Action<string,Exception?> 診断書込 = 起動診断を有効にするか
            ? 起動診断.書き込む
            : static (_,_) => { };
        this.カタログサービス=カタログサービス;
        this.CPUトポロジーサービス=CPUトポロジーサービス;
        this.実行サービス=実行サービス;
        this.比較サービス=比較サービス;
        this.UI実行ストア=UI実行ストア;
        this.UI実行リポジトリ=new ベンチマークUI実行リポジトリ(UI実行ストア,診断書込);
        this.UIディスパッチャー=UIディスパッチャー??new MauiベンチマークUIディスパッチャー();
        this.UI設定=UI設定??new MauiベンチマークUI設定();
        this.実行イベントパイプライン=new ベンチマーク実行イベントパイプライン();
        this.パイプライン診断=new ベンチマークUI診断(診断書込);
        this.描画スケジューラー=new ベンチマークUI描画スケジューラー(this.UIディスパッチャー,this.実行イベントパイプライン,this.パイプライン診断,this.実行UIスナップショットを適用);
        this.永続化バッチ処理=new ベンチマーク永続化バッチ処理(this.UIディスパッチャー,this.パイプライン診断,this.ベンチマーク結果一覧保存をキューへ追加);
        if(起動診断を有効にするか) {
            this.パイプライン診断.ハートビートを開始(this.UIディスパッチャー);
        }
        this.ベンチマークルート=ベンチマークルート;
        this.ソースタブ一覧=ベンチマークソースタブ一覧.全て.Select(static 項目 => new ソースタブ選択肢(項目)).ToArray();
        this.ソースタブ表示状態一覧値=this.ソースタブ一覧.Select(static 項目 => new ベンチマークソースタブ表示状態(項目.定義)).ToArray();
        foreach(var 状態 in this.ソースタブ表示状態一覧値) {
            this.タブ別ソースタブ表示状態[状態.定義.タブ]=状態;
        }
        this.選択ソースタブ値=this.ソースタブ一覧[0];
        this.ソースタブ表示選択を適用();
        this.ベンチマーク列選択グループ一覧=this.ベンチマーク列選択グループ一覧を作成();
        this.ベンチマークプロジェクト選択グループ一覧=this.ベンチマークプロジェクト選択グループ一覧を作成();
        this.保存ソート規則スナップショット一覧=this.保存ベンチマークソート規則を読み込む();
        this.ベンチマークソート列選択肢を更新();
        this.ベンチマークソート規則を既定へ戻す(false);
        this.実行プロファイル一覧=実行プロファイル一覧を作成();
        this.選択実行プロファイル値=this.実行プロファイル一覧.First(static 項目 => 項目.プロファイル==ベンチマーク実行プロファイル.高速);
        var 初期実行選択肢 = 新規実行選択肢を作成(this.選択実行プロファイル値.プロファイル);
        this.実行選択肢一覧.Add(初期実行選択肢);
        this.選択実行選択肢値=初期実行選択肢;
        this.閾値選択肢一覧=閾値選択肢を作成();
        this.選択閾値値=this.閾値選択肢一覧[0];
        this.全選択コマンド=new RelayCommand(
            () => this.全ベンチマーク選択を設定(true),
            () => this.ベンチマーク選択を変更できるか);
        this.選択クリアコマンド=new RelayCommand(
            () => this.全ベンチマーク選択を設定(false),
            () => this.ベンチマーク選択を変更できるか);
        this.実行コマンド=new AsyncRelayCommand(this.実行または再開Async,() => this.ベンチマークを実行可能か);
        this.DB保存コマンド=new AsyncRelayCommand(this.DBを保存Async,() => this.DB保存可能か);
        this.新規グリッドコマンド=new RelayCommand(this.新規グリッドを作成,() => this.新規グリッド作成可能か);
#if DEBUG
        this.無限実行コマンド=new AsyncRelayCommand(this.無限実行Async,() => this.ベンチマークを実行可能か);
#endif
        this.実行履歴クリアコマンド=new AsyncRelayCommand(this.実行履歴をクリアAsync,() => this.実行履歴をクリア可能か);
        this.実行切替コマンド=new RelayCommand(this.実行を切り替え,() => this.実行ツールバーボタン使用可能か);
        this.停止コマンド=new AsyncRelayCommand(this.停止Async,() => this.ベンチマークを停止可能か);
        this.一時停止コマンド=new AsyncRelayCommand(this.一時停止Async,() => this.ベンチマークを停止可能か);
        this.キャンセルコマンド=new RelayCommand(this.キャンセル);
        this.改善プロンプトコピーコマンド=new AsyncRelayCommand(this.改善プロンプトをコピーAsync,() => this.改善プロンプトをコピー可能か);
        this.速度改善プロンプトコピーコマンド=new AsyncRelayCommand(this.速度改善プロンプトをコピーAsync,() => this.速度改善プロンプトをコピー可能か);
        this.メモリ改善プロンプトコピーコマンド=new AsyncRelayCommand(this.メモリ改善プロンプトをコピーAsync,() => this.メモリ改善プロンプトをコピー可能か);
        if(起動診断を有効にするか) {
            起動診断.既存ログから初期化();
            this.ログ文字列値=起動診断.ログ文字列を取得();
            this.ログ購読=起動診断.購読(this.ログ文字列を追加);
            if(!string.IsNullOrWhiteSpace(this.ログ文字列値)) {
                this.ログ項目一覧.Add(this.ログ文字列値);
            }
        }

        foreach(var CPU in this.CPUトポロジーサービス.読み込む()) {
            this.CPU項目一覧.Add(CPU);
            CPU.PropertyChanged+=(_,引数) => {
                if(引数.PropertyName==nameof(CPU選択項目.選択中か)&&!this.CPU選択適用中か) {
                    if(this.更新中か) {
                        this.CPU選択適用中か=true;
                        try {
                            foreach(var 項目 in this.CPU項目一覧) {
                                項目.選択中か=this.選択論理CPU一覧.Contains(項目.論理プロセッサインデックス);
                            }
                        } finally {
                            this.CPU選択適用中か=false;
                            this.CPU区分状態を更新();
                        }
                        return;
                    }

                    this.選択論理CPUを更新();
                    this.CPU区分状態を更新();
                }
            };
        }
        this.CPU区分を再構築();
        this.選択論理CPUを更新();
    }

    /// <summary>
    /// ベンチマークプロジェクト見出し文字列を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークプロジェクト見出し文字列を更新() {
        foreach(var グループ in this.ベンチマークプロジェクト選択グループ一覧) {
            if(グループ.ID=="左") {
                グループ.見出し文字列を設定(this.選択ソース定義.左見出し);
            } else if(グループ.ID=="右") {
                グループ.見出し文字列を設定(this.選択ソース定義.右見出し);
            }
        }
    }

    /// <summary>
    /// 結果項目一覧 プロパティです。
    /// </summary>
    /// <value>結果項目一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク結果項目> 結果項目一覧 { get; } = [];

    /// <summary>
    /// ログ項目一覧 プロパティです。
    /// </summary>
    /// <value>ログ項目一覧 の値を取得/設定します。</value>
    public ObservableCollection<string> ログ項目一覧 { get; } = [];

    /// <summary>
    /// ログ文字列 プロパティです。
    /// </summary>
    /// <value>ログ文字列 の値を取得/設定します。</value>
    public string ログ文字列 {
        get => this.ログ文字列値;
        private set => this.SetProperty(ref this.ログ文字列値,value);
    }

    /// <summary>
    /// 更新中か プロパティです。
    /// </summary>
    /// <value>更新中か の値を取得/設定します。</value>
    public bool 更新中か {
        get => this.内部更新中か;
        private set {
            if(this.SetProperty(ref this.内部更新中か,value)) {
                this.実行コマンド.NotifyCanExecuteChanged();
                this.DB保存コマンド.NotifyCanExecuteChanged();
                this.新規グリッドコマンド.NotifyCanExecuteChanged();
#if DEBUG
                this.無限実行コマンド.NotifyCanExecuteChanged();
#endif
                this.実行履歴クリアコマンド.NotifyCanExecuteChanged();
                this.実行切替コマンド.NotifyCanExecuteChanged();
                this.停止コマンド.NotifyCanExecuteChanged();
                this.一時停止コマンド.NotifyCanExecuteChanged();
                this.ツールバーコマンド状態を更新();
                this.全選択コマンド.NotifyCanExecuteChanged();
                this.選択クリアコマンド.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.ベンチマーク選択を変更できるか));
                this.OnPropertyChanged(nameof(this.ベンチマーク条件を変更可能か));
                this.OnPropertyChanged(nameof(this.実行履歴を選択可能か));
                this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
                this.OnPropertyChanged(nameof(this.ベンチマークを再開可能か));
                this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
                this.OnPropertyChanged(nameof(this.DB保存可能か));
                this.OnPropertyChanged(nameof(this.新規グリッド作成可能か));
                this.OnPropertyChanged(nameof(this.ベンチマークを停止可能か));
                this.OnPropertyChanged(nameof(this.実行履歴をクリア可能か));
                this.OnPropertyChanged(nameof(this.実行履歴クリアツールチップ文字列));
                this.OnPropertyChanged(nameof(this.ツールバー状態入力));
            }
        }
    }

    /// <summary>
    /// ベンチマーク条件を変更可能か プロパティです。
    /// </summary>
    /// <value>ベンチマーク条件を変更可能か の値を取得/設定します。</value>
    public bool ベンチマーク条件を変更可能か =>
        !this.更新中か
        &&!this.実行履歴読込中か
        &&!this.選択実行DB読込中か
        &&!this.ベンチマーク実行有効か
        &&!this.ベンチマーク一時停止中か;

    /// <summary>
    /// ベンチマークを実行可能か プロパティです。
    /// </summary>
    /// <value>ベンチマークを実行可能か の値を取得/設定します。</value>
    public bool ベンチマークを実行可能か =>
        !this.更新中か
        &&!this.実行履歴読込中か
        &&!this.選択実行DB読込中か
        &&!this.実行選択肢選択適用中か
        &&!this.ベンチマーク実行中か
        &&(this.選択ベンチマークセルありか||保存DB実行選択肢か(this.選択実行選択肢));

    /// <summary>
    /// DB保存可能か プロパティです。
    /// </summary>
    /// <value>DB保存可能か の値を取得/設定します。</value>
    public bool DB保存可能か =>
        !this.更新中か
        &&!this.実行履歴読込中か
        &&!this.選択実行DB読込中か
        &&!this.ベンチマーク実行有効か
        &&!this.ベンチマーク一時停止中か
        &&新規実行選択肢か(this.選択実行選択肢)
        &&this.保存可能ベンチマーク結果があるか;

    /// <summary>
    /// 終了状態保存保留ありか プロパティです。
    /// </summary>
    /// <value>終了状態保存保留ありか の値を取得/設定します。</value>
    public bool 終了状態保存保留ありか {
        get => this.内部終了状態保存保留ありか;
        private set => this.SetProperty(ref this.内部終了状態保存保留ありか,value);
    }

    /// <summary>
    /// 未保存ベンチマーク結果ありか プロパティです。
    /// </summary>
    /// <value>未保存ベンチマーク結果ありか の値を取得/設定します。</value>
    public bool 未保存ベンチマーク結果ありか {
        get => this.内部未保存ベンチマーク結果ありか;
        private set {
            if(this.SetProperty(ref this.内部未保存ベンチマーク結果ありか,value)) {
                this.DB保存コマンド.NotifyCanExecuteChanged();
                this.ツールバーコマンド状態を更新();
                this.OnPropertyChanged(nameof(this.ツールバー状態入力));
                this.OnPropertyChanged(nameof(this.DB保存可能か));
            }
        }
    }

    /// <summary>
    /// 最新ベンチマーク自動スクロール要求 プロパティです。
    /// </summary>
    /// <value>最新ベンチマーク自動スクロール要求 の値を取得/設定します。</value>
    public ベンチマーク自動スクロール要求? 最新ベンチマーク自動スクロール要求 { get; private set; }

    /// <summary>
    /// ソースタブを選択Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="選択肢">選択肢 を指定して実行条件を制御します。</param>
    private async Task ソースタブを選択Async(ソースタブ選択肢 選択肢) {
        await Task.CompletedTask;
        this.現在ベンチマーク選択を取得();
        this.ソースタブ選択を適用(選択肢);
    }

    /// <summary>
    /// 現在要素数文字列 プロパティです。
    /// </summary>
    /// <value>現在要素数文字列 の値を取得/設定します。</value>
    public string 現在要素数文字列 {
        get => this.現在要素数文字列値;
        private set => this.SetProperty(ref this.現在要素数文字列値,value);
    }

    /// <summary>
    /// 初期化Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task 初期化Async() {
        await this.更新Async();
    }

    /// <summary>
    /// 更新Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 更新Async() {
        this.更新中か=true;
        this.状態文字列="読み込み中";
        起動診断.書き込む("ベンチマークカタログの更新を開始しました。");
        var DB利用不可か = false;
        try {
            await this.DB実行一覧を更新Async(CancellationToken.None,true,新規選択を強制するか: true);
        } catch(Exception 例外) {
            DB利用不可か=true;
            起動診断.書き込む("ベンチマーク実行一覧の更新に失敗しました。",例外);
        }
        try {
            this.現在ベンチマーク選択を取得();
            var 読込済み = await this.カタログサービス.読込Async(CancellationToken.None);
            this.カタログケース一覧=読込済み;
            this.ベンチマークケースインデックスを更新(読込済み);
            this.カタログからベンチマーク行を再構築();
            this.実行選択を復元(null,null);
            this.ベンチマーク選択集計を更新();
            if(!this.選択タブの現在実行表示を復元()) {
                this.表示結果を更新();
                this.選択要素数を表へ適用();
                this.改善プロンプトを更新();
            }
            this.ソースタブ進行を更新();
            this.現在ソートを適用();
            this.状態文字列=this.ベンチマーク行一覧.Count+" 件のベンチマーク"+(DB利用不可か ? "(実行履歴DB利用不可)" : "");
            起動診断.書き込む($"ベンチマークカタログを読み込みました。 行一覧={this.ベンチマーク行一覧.Count} DB利用不可={DB利用不可か}");
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマークカタログの読み込みに失敗しました。",例外);
            this.状態文字列="読み込み失敗 "+例外.Message;
        } finally {
            this.更新中か=false;
            this.保留ソースタブ選択を適用();
        }
    }

    /// <summary>
    /// 同じキーに複数の異なる行が登録されようとした場合(例: ジェネリック型違いで同名メソッドを持つ行が
    /// 型完全名なしのワイルドカードキー/プロジェクト+メソッド名キーで衝突する場合)、そのキーをどちらの行にも
    /// 安全に紐付けられない「曖昧」な状態として無効化する(値をnullにする)。
    /// </summary>
    private void 曖昧さを考慮して登録(Dictionary<string,ベンチマーク表行?> 辞書,string キー,ベンチマーク表行 行) {
        if(辞書.TryGetValue(キー,out var 既存)) {
            if(既存 is not null&&!ReferenceEquals(既存,行)) {
                辞書[キー]=null;
            }
            return;
        }
        辞書[キー]=行;
    }

    /// <summary>
    /// UI状態を保存 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void UI状態を保存() {
        this.現在ベンチマーク選択を取得();
        this.選択実行状態選択キャッシュを更新();
        if(this.ベンチマーク実行有効か) {
            this.今回実行ベンチマーク選択スナップショットを更新();
        }

        this.終了状態保存保留ありか=false;
        if(!this.UI状態適用中か) {
            this.ベンチマーク選択表示を更新();
        }
    }

    /// <summary>
    /// 未保存ベンチマーク結果を破棄 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void 未保存ベンチマーク結果を破棄() {
        var 破棄済み実行ID一覧 = new HashSet<long>();
        if(this.有効UI実行ID is { } 有効実行ID) {
            破棄済み実行ID一覧.Add(有効実行ID);
        }
        if(this.未保存UI実行ID is { } 未保存実行ID) {
            破棄済み実行ID一覧.Add(未保存実行ID);
        }
        foreach(var 破棄済み実行ID in 破棄済み実行ID一覧) {
            this.UI実行リポジトリ.実行削除を予約(破棄済み実行ID);
        }
        this.有効UI実行ID=null;
        this.未保存UI実行ID=null;
        this.未保存ベンチマーク結果ありか=false;
        this.終了状態保存保留ありか=false;
        this.現在未保存実行ルート=null;
        this.ツールバーコマンド状態を更新();
    }

    /// <summary>
    /// 条件に一致する行が複数存在する場合は曖昧として null を返す(誤って別の行へ結果を紐付けないため)。
    /// ジェネリック型違い(例: Tsavorite&lt;int&gt; / Tsavorite&lt;ClassInt32&gt;)で同名メソッドを持つ行が
    /// 複数存在するケースで、型情報が欠落した結果/ジョブがどちらか一方へ誤配線されるのを防ぐ。
    /// </summary>
    private static ベンチマーク表行? 一意な候補行(IEnumerable<ベンチマーク表行> 候補一覧,Func<ベンチマーク表行,bool> 条件) {
        ベンチマーク表行? 一致 = null;
        foreach(var 候補 in 候補一覧) {
            if(!条件(候補)) {
                continue;
            }
            if(一致 is not null) {
                return null;
            }
            一致 = 候補;
        }
        return 一致;
    }

    /// <summary>
    /// 行の結果か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 行の結果か(ベンチマーク表行 行,ベンチマーク結果項目 結果) {
        if(行.プロジェクト別ケース.TryGetValue(結果.プロジェクト,out var ベンチマーク)) {
            return string.Equals(ベンチマーク.メソッド名,結果.メソッド,StringComparison.OrdinalIgnoreCase)
                &&行の型と一致するか(行,結果.型完全名,ベンチマーク.型完全名)
                &&行の引数と一致するか(行.ソースタブ.タブ,ベンチマーク.引数キー,結果.引数キー);
        }

        return false;
    }

    /// <summary>
    /// 行の型と一致するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="実際型完全名">実際型完全名 を指定して実行条件を制御します。</param>
    /// <param name="期待型完全名">期待型完全名 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 行の型と一致するか(ベンチマーク表行 行,string 実際型完全名,string 期待型完全名) {
        if(スケーラブルソースタブか(行.ソースタブ.タブ)) {
            return !string.IsNullOrWhiteSpace(実際型完全名)
                &&string.Equals(期待型完全名,実際型完全名,StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(実際型完全名)
            ||string.Equals(期待型完全名,実際型完全名,StringComparison.OrdinalIgnoreCase);
    }

    public async Task 終了準備Async() {
        if(this.終了処理中か) {
            return;
        }

        this.終了処理中か=true;
        this.ログ購読?.Dispose();
        this.ログ購読=null;
        await this.パイプライン診断.ハートビートを停止Async();
        try {
            this.永続化バッチ処理.即時反映();
            await this.UI実行リポジトリ.保存完了を待機Async();
        } catch(Exception 例外) {
            System.Diagnostics.Debug.WriteLine("ベンチマークUI終了時の永続化フラッシュに失敗しました: "+例外);
        }
    }

    /// <summary>
    /// DBを保存Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task DBを保存Async() {
        if(!this.DB保存可能か) {
            return;
        }

        this.更新中か=true;
        try {
            this.表示結果を現在実行セッションへ昇格();
            this.現在ベンチマーク選択を取得();
            this.今回実行ベンチマーク選択スナップショットを更新();
            var 実行ソースタブ = this.選択ソース定義.タブ;
            var 実行プロファイル = this.選択実行プロファイル.プロファイル;
            var 実行ルート = this.現在未保存実行ルート??this.未保存実行ルートを作成(実行プロファイル);
            var 実行ID = this.未保存UI実行ID??this.有効UI実行ID??await this.UI実行を作成Async(実行ソースタブ,実行プロファイル);
            if(実行ID is null) {
                this.状態文字列="DB保存失敗: 実行を作成できませんでした";
                return;
            }

            var 要素件数一覧 = this.選択要素数一覧を取得();
            var 論理CPU一覧 = this.選択論理CPUを取得();
            var スナップショット項目一覧 = this.ベンチマークグリッドスナップショット項目一覧を作成();
            var 保存ソースタブ = this.選択ソース定義;
            var 保存結果一覧 = this.全グリッド結果項目を取得()
                .Where(結果 => 結果.ソースタブ==保存ソースタブ.タブ);
            var 保存進行状態 = this.結果から進行状態を作成(
                実行ルート,
                保存ソースタブ,
                保存結果一覧);
            new ベンチマーク状態マネージャー(this.ベンチマークルートを取得(実行プロファイル)).保存(保存進行状態);
            this.UI実行リポジトリ.実行設定保存を予約(実行ID,実行ルート,要素件数一覧,this.適応閾値を取得(),論理CPU一覧);
            this.UI実行リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,スナップショット項目一覧);
            this.UI実行状態をキューへ追加(実行ID,UI実行状態.完了,"ベンチマークグリッドスナップショットを保存しました。");
            await this.UI実行リポジトリ.保存完了を待機Async();
            this.未保存UI実行ID=null;
            this.有効UI実行ID=null;
            this.選択実行状態キャッシュ=null;
            this.未保存ベンチマーク結果ありか=false;
            this.終了状態保存保留ありか=false;
            this.現在未保存実行ルート=null;
            this.状態文字列="DB保存しました";
            await this.DB実行一覧を更新Async(CancellationToken.None,false,実行ID,true);
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマークDBの保存に失敗しました。",例外);
            this.状態文字列="DB保存失敗 "+例外.Message;
        } finally {
            this.更新中か=false;
            this.ツールバーコマンド状態を更新();
        }
    }

    /// <summary>
    /// ベンチマーク結果保存をキューへ追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <param name="実行世代">実行世代 を指定して実行条件を制御します。</param>
    /// <param name="実行ID">実行ID を指定して実行条件を制御します。</param>
    private void ベンチマーク結果保存をキューへ追加(ベンチマーク結果項目 結果,int 実行世代,long? 実行ID) {
        if(実行ID is null) {
            return;
        }

        this.永続化バッチ処理.追加(new ベンチマーク永続化項目(結果,実行世代,実行ID));
    }

    /// <summary>
    /// ベンチマーク結果一覧保存をキューへ追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="項目一覧">項目一覧 を指定して実行条件を制御します。</param>
    private void ベンチマーク結果一覧保存をキューへ追加(IReadOnlyList<ベンチマーク永続化項目> 項目一覧) {
        if(項目一覧.Count==0) {
            return;
        }

        UIスレッド予算.計測("persist-enqueue",() => {
            var グリッド結果一覧 = this.永続化用グリッドスナップショット結果を作成(項目一覧);
            foreach(var 項目 in 項目一覧) {
                if(項目.実行世代!=this.実行結果世代) {
                    continue;
                }

                var 行 = this.ベンチマーク行を検索(項目.結果);
                if(行 is not null&&this.ベンチマークグリッドスナップショット項目を作成(行,0,グリッド結果一覧) is { } スナップショット項目) {
                    this.UI実行リポジトリ.ベンチマークスナップショット項目保存を予約(項目.UI実行ID,[スナップショット項目]);
                }
            }
        });
    }

    /// <summary>
    /// UI実行アプリ種別を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private static UI実行アプリ種別 UI実行アプリ種別を取得() {
#if SCALABLE_BENCHMARK
        return UI実行アプリ種別.スケーラブルベンチマーク;
#else
        return UI実行アプリ種別.ベンチマーク;
#endif
    }

    /// <summary>
    /// キャンセル メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void キャンセル() {
        this.実行キャンセル?.Cancel();
    }

    /// <summary>
    /// 実行結果を受理 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <param name="実行世代">実行世代 を指定して実行条件を制御します。</param>
    /// <param name="UI実行ID">UI実行ID を指定して実行条件を制御します。</param>
    private void 実行結果を受理(ベンチマーク結果項目 結果,int 実行世代,long? UI実行ID) {
        if(実行世代!=this.実行結果世代) {
            return;
        }
        if(実行中結果か(結果)&&!this.ベンチマーク実行有効か) {
            return;
        }

        this.パイプライン診断.受信イベントを記録();
        if(終了結果か(結果)) {
            結果.実行内測定か=true;
        }
        if(実行中結果か(結果)) {
            this.即時実行中結果をキューへ追加(結果,実行世代);
        }

        this.実行イベントパイプライン.イベントを送信(new ベンチマーク実行イベント(結果,実行世代,UI実行ID,DateTimeOffset.UtcNow));
        this.描画スケジューラー.描画を要求();
    }

    /// <summary>
    /// 実行結果を処理 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <param name="実行世代">実行世代 を指定して実行条件を制御します。</param>
    /// <param name="UI実行ID">UI実行ID を指定して実行条件を制御します。</param>
    private void 実行結果を処理(ベンチマーク結果項目 結果,int 実行世代,long? UI実行ID) {
        if(実行世代!=this.実行結果世代) {
            return;
        }

        起動診断.パンくずを追加(this.結果パンくずを作成("実行結果を処理",結果));
        起動診断.書き込む(
            "実行結果を受信しました。 "
            +"プロジェクト="+結果.プロジェクト+" "
            +"メソッド="+結果.メソッド+" "
            +"型="+(string.IsNullOrWhiteSpace(結果.型完全名) ? "-" : 結果.型完全名)+" "
            +"N="+結果.要素数.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"ループ="+結果.ループ回数.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"CPU="+(結果.割り当て論理CPU?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")+" "
            +"状態="+結果.状態+" "
            +"平均="+結果.平均+" "
            +"割り当て="+結果.割り当てバイト数文字列);
        var CPU = this.割当CPUを検索(結果);
        var 結果ソースタブ = 結果.ソースタブ;
        var 表示タブ結果か = 結果ソースタブ==this.選択ソース定義.タブ;
        var 実行中結果か = ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.実行中);
        var 完了結果か = string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase);
        var 失敗結果か = string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);
        var 終端結果か = 完了結果か||失敗結果か;
        if(実行中結果か&&!this.ベンチマーク実行有効か) {
            return;
        }

        var 結果要素数ありか = 結果.軸数>0;
        var ソースタブ進行を更新するべきか = false;
        var 行 = this.ベンチマーク行を検索(結果);
        if(実行中結果か||終端結果か) {
            CPU?.ジョブを適用(結果,行?.ベンチマークメソッド文字列);
            this.ソースタブ実行中結果を適用(結果);
        }
        if(実行中結果か) {
            this.有効実行ソースタブを記録(結果ソースタブ);
        }

        if(失敗結果か) {
            起動診断.書き込む(ベンチマーク失敗表示ロジック.ログ文を作成(
                結果.プロジェクト,
                結果.メソッド,
                結果.軸数,
                結果.割り当て論理CPU,
                結果.成果物フォルダー,
                結果.失敗理由,
                結果.型完全名));
        }
        if(表示タブ結果か&&結果.軸数>0) {
            this.現在要素数文字列=要素数文字列を整形(結果.軸数);
        }
        if(実行中結果か&&結果要素数ありか) {
            this.現在実行結果項目を追加または更新(結果);
            ソースタブ進行を更新するべきか=true;
        }
        if(表示タブ結果か&&実行中結果か) {
            this.ベンチマーク自動スクロールを要求(結果ソースタブ,結果.メソッド,行?.行キー??string.Empty);
            this.実行要素の完了強調を維持(結果.軸数);
            行?.実行結果を適用(結果,CPU,DateTimeOffset.Now);
            return;
        }
        if(実行中結果か) {
            return;
        }

        if(終端結果か&&結果要素数ありか) {
            this.現在実行結果項目を追加または更新(結果);
            this.結果確認を更新(結果ソースタブ);
            ソースタブ進行を更新するべきか=true;
        }
        if(表示タブ結果か&&終端結果か&&結果要素数ありか) {
            this.結果項目を追加または更新(結果);
            this.結果要素数を登録(結果.軸数);
            this.結果確認を更新(結果ソースタブ);
            this.表示結果を更新();
        }
        var 完了日時 = DateTimeOffset.Now;
        if(表示タブ結果か) {
            行?.実行結果を適用(結果,CPU,完了日時);
        }
        if(終端結果か) {
            this.終了状態保存保留ありか=true;
            this.未保存ベンチマーク結果ありか=true;
            ソースタブ進行を更新するべきか=true;
            if(表示タブ結果か) {
                this.選択要素数を表へ適用();
                行?.実行結果を適用(結果,CPU,完了日時);
                this.最終完了状態を更新();
            }
            this.ベンチマーク結果保存をキューへ追加(結果,実行世代,UI実行ID);
        }
        if(ソースタブ進行を更新するべきか) {
            this.ソースタブ進行を更新();
        }
    }

    /// <summary>
    /// 保留実行更新を破棄するか プロパティです。
    /// </summary>
    /// <value>保留実行更新を破棄するか の値を取得/設定します。</value>
    private void 保留最終ライブ表示クリアの適用を試みる() {
        if(!this.最終ライブ表示クリア保留中か||this.ベンチマーク実行有効か||this.実行イベントパイプライン.保留ありか) {
            return;
        }

        var 破棄するか = this.最終ライブ表示クリア破棄保留中か;
        this.最終ライブ表示クリア保留中か=false;
        this.最終ライブ表示クリア破棄保留中か=false;
        this.最終ライブ表示クリアを適用(破棄するか);
    }

    private async Task 実行イベントパイプラインを排出Async() {
        for(var 再試行 = 0;再試行<200;再試行++) {
            this.描画スケジューラー.即時反映();
            if(!this.実行イベントパイプライン.保留ありか) {
                return;
            }

            await Task.Delay(5);
        }
    }

    private void 保留最終ライブ表示クリアを予約(int 実行世代) {
        _=Task.Run(async () => {
            for(var 再試行 = 0;再試行<200;再試行++) {
                await Task.Delay(10);
                if(this.実行イベントパイプライン.保留ありか) {
                    continue;
                }

                this.UIディスパッチャー.呼び出し開始(() => {
                    if(実行世代!=this.実行結果世代) {
                        return;
                    }

                    this.保留最終ライブ表示クリアの適用を試みる();
                });
                return;
            }
        });
    }

    private void 最終ライブ表示クリアを適用(bool 保留実行更新を破棄するか = false) {
        起動診断.パンくずを追加(
            "最終ライブ表示クリアを適用 破棄="
            +保留実行更新を破棄するか.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" 選択="+this.選択ソース定義.タブ
            +" 有効="+(this.有効実行ソースタブ?.ToString()??"-"));
        this.最終ライブ表示クリアトークン=unchecked(this.最終ライブ表示クリアトークン+1);
        if(保留実行更新を破棄するか) {
            this.実行イベントパイプライン.クリア();
        }

        this.現在実行セッション.実行中結果をクリア();
        this.ソースタブ実行中状態をクリア();
        this.ベンチマークライブ状態をクリア();
        this.選択要素数を表へ適用();
        this.最新完了ライブ結果を復元();
        this.CPUジョブをクリア();
        // 持ち越し終端イベントが1件も無い場合は 実行UIスナップショットを適用 がもう呼ばれないため、ここでも判定する。
        this.実行後表示確定の適用を試みる();
    }

    /// <summary>
    /// ベンチマークライブ状態をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークライブ状態をクリア() {
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            行.実行時状態をクリア();
        }
    }

    private string 結果パンくずを作成(string コンテキスト,ベンチマーク結果項目 結果) =>
        コンテキスト
        +" ソース="+結果.ソースタブ
        +" 表示="+this.選択ソース定義.タブ
        +" 有効="+(this.有効実行ソースタブ?.ToString()??"-")
        +" プロジェクト="+結果.プロジェクト
        +" メソッド="+結果.メソッド
        +" 状態="+結果.状態
        +" n="+結果.軸数.ToString(System.Globalization.CultureInfo.InvariantCulture)
        +" CPU="+(結果.割り当て論理CPU?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")
        +" 実行中="+this.ベンチマーク実行有効か.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// 結果項目を追加または更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    private void 結果項目を追加または更新(ベンチマーク結果項目 結果) {
        // 実行が進むほどResultItemsが蓄積するため、RemoveAtを複数回行う実装(旧)は
        // 1件完了するたびにO(n)の走査+シフトが発生し実行後半でUIスレッドが詰まりやすかった。
        // IsResultMatchで一致する項目は識別上高々1件のため、見つかった位置への置換(Replace通知1回)に留める。
        for(var インデックス = 0;インデックス<this.結果項目一覧.Count;インデックス++) {
            if(結果が一致するか(this.結果項目一覧[インデックス],結果)) {
                this.結果項目一覧[インデックス]=結果;
                return;
            }
        }
        this.結果項目一覧.Add(結果);
    }

    /// <summary>
    /// 現在実行結果項目を追加または更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    private void 現在実行結果項目を追加または更新(ベンチマーク結果項目 結果) {
        this.現在実行セッション.結果を更新または追加(結果);
    }

    /// <summary>
    /// 結果確認を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <param name="確認文字列">確認文字列 を指定して実行条件を制御します。</param>
    private void 結果確認を適用(ベンチマーク結果項目 結果,string 確認文字列) {
        結果.結果確認文字列=確認文字列;
        var 行 = this.ベンチマーク行を検索(結果);
        行?.結果確認を適用(結果.プロジェクト,確認文字列,結果.結果シード文字列);
        foreach(var 項目 in this.結果項目一覧.ToArray().Where(項目 => 結果が一致するか(項目,結果))) {
            項目.結果確認文字列=確認文字列;
        }
        foreach(var 項目 in this.現在実行結果項目一覧を取得(結果.ソースタブ).ToArray()
            .Where(項目 => 結果が一致するか(項目,結果))) {
            項目.結果確認文字列=確認文字列;
        }
    }

    /// <summary>
    /// ベンチマークルートを取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="プロファイル">プロファイル を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string ベンチマークルートを取得(ベンチマーク実行プロファイル プロファイル) =>
        Path.Combine(this.ベンチマークルート,ベンチマーク実行プロファイル名.フォルダー名に変換(プロファイル));

    /// <summary>
    /// 適応閾値を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private TimeSpan 適応閾値を取得() {
        return TimeSpan.FromMilliseconds(this.選択閾値.ミリ秒);
    }

    /// <summary>
    /// 結果要素数を登録 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    private void 結果要素数を登録(int 要素数) {
        if(要素数<=0||this.結果要素数選択肢一覧.Any(項目 => 項目.値==要素数)) {
            return;
        }

        var 選択肢 = new 結果要素数選択肢(要素数,this.結果要素数表示名を整形(要素数));
        var 並び替え済み = this.結果要素数選択肢一覧
            .Append(選択肢)
            .OrderBy(static 項目 => 項目.値)
            .ToArray();
        this.結果要素数選択肢一覧.Clear();
        foreach(var 項目 in 並び替え済み) {
            this.結果要素数選択肢一覧.Add(項目);
        }
        if(!this.実行中か&&this.選択結果要素数 is null) {
            this.選択結果要素数=this.結果要素数選択肢一覧.FirstOrDefault();
        }
    }

    /// <summary>
    /// 要素数文字列を整形 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string 要素数文字列を整形(int 要素数) {
        return 要素数>0
            ? 要素数.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "-";
    }

    /// <summary>
    /// 結果要素数表示名を整形 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string 結果要素数表示名を整形(int 要素数) =>
        ベンチマーク作業軸.ループ回数を使うか(this.選択ソース定義.タブ)
            ? "ループ回数 "+要素数.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : 要素数==ベンチマーク要素数一覧.基本要素数
                ? "N"
                : 要素数.ToString(System.Globalization.CultureInfo.InvariantCulture)+"N";

    /// <summary>
    /// 結果要素数一覧を再構築 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="要素件数一覧">要素件数一覧 を指定して実行条件を制御します。</param>
    private void 結果要素数一覧を再構築(IReadOnlyList<int> 要素件数一覧) {
        this.結果要素数選択肢一覧.Clear();
        foreach(var 要素数 in 要素件数一覧.Where(static 項目 => 項目>0).Distinct().Order()) {
            this.結果要素数選択肢一覧.Add(new 結果要素数選択肢(要素数,this.結果要素数表示名を整形(要素数)));
        }

        this.選択結果要素数=this.結果要素数選択肢一覧.LastOrDefault();
    }

    /// <summary>
    /// 保存可能ベンチマーク結果があるか プロパティです。
    /// </summary>
    /// <value>保存可能ベンチマーク結果があるか の値を取得/設定します。</value>
    private bool 保存可能ベンチマーク結果があるか =>
        this.全グリッド結果項目を取得().Any(終了結果か);

    private static bool 終了結果か(ベンチマーク結果項目 結果) =>
        string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 現在実行結果を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果一覧">結果一覧 を指定して実行条件を制御します。</param>
    private void 現在実行結果を表へ適用(IReadOnlyList<ベンチマーク結果項目> 結果一覧) {
        var 選択値要素数 = this.選択結果要素数?.値;
        foreach(var 結果 in 結果一覧.Where(結果 => 選択値要素数 is null||結果.軸数==選択値要素数)) {
            var 行 = this.ベンチマーク行を検索(結果);
            行?.実行結果を適用(結果,this.割当CPUを検索(結果),結果.開始日時??DateTimeOffset.Now,false);
        }
    }

    /// <summary>
    /// 新規実行用に全現在実行結果をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 新規実行用に全現在実行結果をクリア() {
        this.ソースタブ実行中状態をクリア();
        this.現在実行セッション.クリア();
        this.全ソースタブ比較表示をクリア();
        this.結果項目一覧.Clear();
        this.表示結果項目一覧.Clear();
        this.結果要素数選択肢一覧.Clear();
        this.選択結果要素数=null;
        this.現在要素数文字列="-";
        this.ベンチマーク結果表示をクリア();
        this.改善プロンプト文字列=string.Empty;
        this.速度改善プロンプト文字列=string.Empty;
        this.メモリ改善プロンプト文字列=string.Empty;
        this.選択実行状態キャッシュ=null;
        this.実行結果強調をクリア();
        this.ソースタブ進行を更新();
        this.ツールバーコマンド状態を更新();
    }

    /// <summary>
    /// 実行結果強調をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 実行結果強調をクリア() {
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            行.実行強調をクリア();
        }
    }

    /// <summary>
    /// ベンチマーク結果表示をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマーク結果表示をクリア() {
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            行.結果表示をクリア();
        }
        this.最終完了状態をクリア();
    }

    /// <summary>
    /// 今回実行メモリ表示を維持するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool 今回実行メモリ表示を維持するか() =>
        ベンチマーク実行表示元ロジック.今回実行メモリ表示を維持するか(
            this.現在実行セッション.結果ありか);

    /// <summary>
    /// 表示測定一覧を作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク要素測定[] 表示測定一覧を作成() =>
        this.表示測定一覧を作成(this.表示用結果ソースを取得());

    private ベンチマーク要素測定[] 表示測定一覧を作成(IReadOnlyList<ベンチマーク結果項目> 結果一覧) =>
        結果一覧
            .Select(static 結果 => new ベンチマーク要素測定(
                結果.プロジェクト,
                結果.メソッド,
                結果.型完全名,
                結果.引数キー,
                結果.軸数,
                結果.状態,
                結果.平均ナノ秒,
                結果.合計ナノ秒))
            .ToArray();

    /// <summary>
    /// IReadOnlyList< メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="左プロジェクト">左プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="右プロジェクト">右プロジェクト を指定して実行条件を制御します。</param>
    private IReadOnlyList<(string 左プロジェクト,string 右プロジェクト)> ベンチマークペア一覧を取得() =>
        ベンチマークペア一覧を作成(this.選択ソース定義);

    /// <summary>
    /// 行の型完全名を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string 行の型完全名を取得(ベンチマーク表行 行,string プロジェクト) =>
        行.プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク)
            ? ベンチマーク.型完全名
            : 行.プロジェクト別ケース.Values.FirstOrDefault()?.型完全名??string.Empty;

    /// <summary>
    /// 行の引数キーを取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string 行の引数キーを取得(ベンチマーク表行 行,string プロジェクト) =>
        行.プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク)
            ? ベンチマーク.引数キー
            : 行.プロジェクト別ケース.Values.FirstOrDefault()?.引数キー??string.Empty;

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private bool 結果があるか(string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") {
        return this.結果があるか(this.表示用結果ソースを取得(),メソッド,プロジェクト,型完全名,要素数,引数キー);
    }

    private bool 結果があるか(IReadOnlyList<ベンチマーク結果項目> 結果一覧,string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") {
        return 結果一覧.Any(結果 => string.Equals(結果.メソッド,メソッド,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(結果.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase)
                &&ベンチマーク型一致ロジック.同じ型か(結果.型完全名,型完全名)
                &&同じ引数か(結果.引数キー,引数キー)
                &&結果.軸数==要素数
                &&string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)
                &&結果.平均ナノ秒 is not null);
    }

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private プロジェクト測定値 結果測定値を取得(string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") {
        return this.結果測定値を取得(this.表示用結果ソースを取得(),メソッド,プロジェクト,型完全名,要素数,引数キー);
    }

    private プロジェクト測定値 結果測定値を取得(IReadOnlyList<ベンチマーク結果項目> 結果一覧,string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") {
        var 選択値実行結果 = 結果一覧
            .LastOrDefault(項目 => string.Equals(項目.メソッド,メソッド,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(項目.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase)
                &&ベンチマーク型一致ロジック.同じ型か(項目.型完全名,型完全名)
                &&同じ引数か(項目.引数キー,引数キー)
                &&項目.軸数==要素数
                &&string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)
                &&項目.平均ナノ秒 is not null);
        return 選択値実行結果 is not null
            ? プロジェクト測定値.測定から作成(選択値実行結果.平均ナノ秒,選択値実行結果.割り当てバイト数値)
            : プロジェクト測定値.欠測;
    }

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private ベンチマーク結果項目? 結果項目を取得(string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") {
        return this.結果項目を取得(this.表示用結果ソースを取得(),メソッド,プロジェクト,型完全名,要素数,引数キー);
    }

    private ベンチマーク結果項目? 結果項目を取得(IReadOnlyList<ベンチマーク結果項目> 結果一覧,string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") {
        for(var インデックス = 結果一覧.Count-1;インデックス>=0;インデックス--) {
            var 項目 = 結果一覧[インデックス];
            if(結果が一致するか(項目,メソッド,プロジェクト,型完全名,要素数,引数キー)
                &&string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)
                &&項目.平均ナノ秒 is not null) {
                return 項目;
            }
        }

        return null;
    }

    /// <summary>
    /// 表示用結果ソースを取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク結果項目> 表示用結果ソースを取得() {
        var ソース = this.表示に今回実行メモリを使うか(this.選択ソース定義)
            ? this.選択タブの現在実行結果項目一覧を取得()
            : this.結果項目一覧;
        return ソース.Where(this.結果が表示選択中か).ToArray();
    }

    private IReadOnlyList<ベンチマーク結果項目> 表適用用結果ソースを取得() {
        var ソース = this.表示に今回実行メモリを使うか(this.選択ソース定義)
            ? this.選択タブの現在実行結果項目一覧を取得()
            : this.結果項目一覧;
        return ソース
            .Where(結果 => this.選択ソース定義.プロジェクトを含むか(結果.プロジェクト))
            .ToArray();
    }

    /// <summary>
    /// ベンチマークメタデータ欠測を報告 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="メソッド">メソッド を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    private void ベンチマークメタデータ欠測を報告(string メソッド,string プロジェクト,int 要素数,ベンチマーク結果項目? 結果) {
        var キー = メソッド+"|"+プロジェクト+"|"+要素数.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(!this.報告済みメタデータ警告.Add(キー)) {
            return;
        }

        var メッセージ = "CPU/時間メタデータ欠測: "
            +ベンチマークフォーマッター.名前を短縮(メソッド)+" / "
            +プロジェクト+" / N="
            +要素数.ToString(System.Globalization.CultureInfo.InvariantCulture);
        this.状態文字列=メッセージ;
        起動診断.書き込む(
            メッセージ+" "
            +"結果あり="+(結果 is null ? "false" : "true")+" "
            +"CPU="+(結果?.割り当て論理CPU?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")+" "
            +"開始日時="+(結果?.開始日時?.ToString("O",System.Globalization.CultureInfo.InvariantCulture)??"-"));
    }

    /// <summary>
    /// 結果が一致するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="項目">項目 を指定して実行条件を制御します。</param>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 結果が一致するか(ベンチマーク結果項目 項目,ベンチマーク結果項目 結果) =>
        結果が一致するか(項目,結果.メソッド,結果.プロジェクト,結果.型完全名,結果.軸数,結果.引数キー);

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private static bool 結果が一致するか(ベンチマーク結果項目 項目,string メソッド,string プロジェクト,string 型完全名,int 要素数,string 引数キー = "") =>
        string.Equals(項目.メソッド,メソッド,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(項目.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase)
        &&ベンチマーク型一致ロジック.同じ型か(項目.型完全名,型完全名)
        &&同じ引数か(項目.引数キー,引数キー)
        &&項目.軸数==要素数;

    /// <summary>
    /// 引数キー プロパティです。
    /// </summary>
    /// <value>引数キー の値を取得/設定します。</value>
    private static string 結果識別キーを作成(string プロジェクト,string 型完全名,string メソッド,int 要素数,string 引数キー = "") =>
        string.Join("|",プロジェクト,型完全名,メソッド,引数キー,要素数.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// 同じ引数か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="左">左 を指定して実行条件を制御します。</param>
    /// <param name="右">右 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 同じ引数か(string 左,string 右) =>
        ベンチマーク引数キー一致ロジック.厳密一致か(左,右);

    /// <summary>
    /// ログ文字列を追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="文字列">文字列 を指定して実行条件を制御します。</param>
    private void ログ文字列を追加(string 文字列) {
        if(this.終了処理中か) {
            return;
        }

        this.UIディスパッチャー.呼び出し開始(() => {
            if(this.終了処理中か) {
                return;
            }

            try {
                this.ログ文字列+=文字列;
                this.ログ項目一覧.Add(文字列);
            } catch(Exception 例外) when(例外 is System.Runtime.InteropServices.COMException or InvalidOperationException) {
                System.Diagnostics.Debug.WriteLine("終了処理中のためベンチマークUIログ追加を無視しました: "+例外);
            }
        });
    }

    // ログ一覧のDataTemplate側でこの接頭辞を検出し、太字／強調色で目立たせる（ログビュー作成を参照）。
    /// <summary>
    /// 強調ログ項目接頭辞 フィールドです。
    /// </summary>
    internal const string 強調ログ項目接頭辞 = "⚠ ";
}
