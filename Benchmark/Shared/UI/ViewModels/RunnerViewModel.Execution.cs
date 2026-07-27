using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// 実行・一時停止・停止・再開
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// 有効実行ルート フィールドです。
    /// </summary>
    private string? 有効実行ルート;

    private int 実行コマンドゲート;

    private int 停止コマンドゲート;

    /// <summary>
    /// ベンチマーク実行中か値 フィールドです。
    /// </summary>
    private bool 内部ベンチマーク実行中か;

    /// <summary>
    /// 実行状態ゲート フィールドです。
    /// </summary>
    private readonly object 実行状態ゲート = new();

    /// <summary>
    /// 保留実行状態文字列 フィールドです。
    /// </summary>
    private string 保留実行状態文字列 = string.Empty;

    /// <summary>
    /// 実行状態更新予約済みか フィールドです。
    /// </summary>
    private int 実行状態更新予約済みか;

    /// <summary>
    /// 現在未保存実行ルート フィールドです。
    /// </summary>
    private string? 現在未保存実行ルート;

    /// <summary>
    /// ベンチマーク実行中か プロパティです。
    /// </summary>
    /// <value>ベンチマーク実行中か の値を取得/設定します。</value>
    public bool ベンチマーク実行中か {
        get => this.内部ベンチマーク実行中か;
        private set {
            if(this.SetProperty(ref this.内部ベンチマーク実行中か,value)) {
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
                this.OnPropertyChanged(nameof(this.実行中か));
                this.OnPropertyChanged(nameof(this.ベンチマーク実行有効か));
                this.OnPropertyChanged(nameof(this.ベンチマーク一時停止中か));
                this.OnPropertyChanged(nameof(this.ソート規則編集可能か));
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
                this.OnPropertyChanged(nameof(this.実行ボタン文字列));
                this.OnPropertyChanged(nameof(this.実行ボタンツールチップ文字列));
                this.OnPropertyChanged(nameof(this.ツールバー状態入力));
            }
        }
    }

    /// <summary>
    /// 実行中か プロパティです。
    /// </summary>
    /// <value>実行中か の値を取得/設定します。</value>
    public bool 実行中か {
        get => this.ベンチマーク実行中か;
        private set => this.ベンチマーク実行中か=value;
    }

    /// <summary>
    /// ベンチマーク実行有効か プロパティです。
    /// </summary>
    /// <value>ベンチマーク実行有効か の値を取得/設定します。</value>
    public bool ベンチマーク実行有効か => this.実行キャンセル is not null&&this.ベンチマーク実行中か;

    /// <summary>
    /// ベンチマーク一時停止中か プロパティです。
    /// </summary>
    /// <value>ベンチマーク一時停止中か の値を取得/設定します。</value>
    public bool ベンチマーク一時停止中か => this.実行キャンセル is null&&this.有効実行ルート is not null;

    public bool ベンチマークを停止可能か =>
        (this.ベンチマーク実行有効か||this.ベンチマーク一時停止中か)
        &&this.保留実行終了種別==実行終了種別.なし;

    /// <summary>
    /// 実行ボタン文字列 プロパティです。
    /// </summary>
    /// <value>実行ボタン文字列 の値を取得/設定します。</value>
    public string 実行ボタン文字列 => "計測";

    /// <summary>
    /// ベンチマークを再開可能か プロパティです。
    /// </summary>
    /// <value>ベンチマークを再開可能か の値を取得/設定します。</value>
    public bool ベンチマークを再開可能か => !this.更新中か&&this.ベンチマーク一時停止中か;

    /// <summary>
    /// 実行ツールバーボタン使用可能か プロパティです。
    /// </summary>
    /// <value>実行ツールバーボタン使用可能か の値を取得/設定します。</value>
    public bool 実行ツールバーボタン使用可能か => this.ベンチマークを実行可能か||this.ベンチマークを再開可能か;

    /// <summary>
    /// 実行ボタンツールチップ文字列 プロパティです。
    /// </summary>
    /// <value>実行ボタンツールチップ文字列 の値を取得/設定します。</value>
    public string 実行ボタンツールチップ文字列 =>
        this.選択ベンチマークセルありか||保存DB実行選択肢か(this.選択実行選択肢)
            ? this.実行ボタン文字列
            : "ベンチマークを1つ以上選択してください";

    /// <summary>
    /// ツールバー状態入力 プロパティです。
    /// </summary>
    /// <value>ツールバー状態入力 の値を取得/設定します。</value>
    internal ベンチマークツールバー状態入力 ツールバー状態入力 => new(
        this.更新中か,
        this.ベンチマーク実行有効か,
        this.ベンチマーク一時停止中か,
        this.ベンチマークを実行可能か,
        this.ベンチマーク実行有効か,
        false,
        this.DB保存可能か,
        this.新規グリッド作成可能か);

    /// <summary>
    /// 指 定 さ れ た 対 象 情 報 を 取 得 し 、 呼 び 出 し 元 へ 返 却 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    /// <param name="役割">対 象 の 引 数 を 受 け 取 り 処 理 を 制 御 す る エ 組 み で す 。</param>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    internal ベンチマークツールバーボタン状態 ツールバーボタン状態を取得(ベンチマークツールバーボタン役割 役割) =>
        ベンチマークツールバー状態ロジック.状態を作成(役割,this.ツールバー状態入力);

    /// <summary>
    /// 一時停止Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task 一時停止Async() {
        if(!this.ベンチマークを停止可能か) {
            return;
        }

        if(Interlocked.Exchange(ref this.停止コマンドゲート,1)==1) {
            return;
        }

        try {
            this.保留実行終了種別=実行終了種別.一時停止;
            this.実行予測を一時停止();
            this.実行コマンド状態変更を通知();
            this.キャンセル();
            await this.実行停止を待機Async();
        } finally {
            this.保留実行終了種別=実行終了種別.なし;
            Interlocked.Exchange(ref this.停止コマンドゲート,0);
            this.実行コマンド状態変更を通知();
        }
    }

    /// <summary>
    /// 停止Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task 停止Async() {
        if(!this.ベンチマークを停止可能か) {
            return;
        }

        if(Interlocked.Exchange(ref this.停止コマンドゲート,1)==1) {
            return;
        }

        try {
            this.保留実行終了種別=実行終了種別.停止;
            this.実行予測をクリア();
            this.実行コマンド状態変更を通知();
            this.キャンセル();
            await this.実行停止を待機Async();
            if(this.ベンチマーク一時停止中か) {
                this.有効実行ルートを設定(null);
            }
            this.保留実行終了種別=実行終了種別.なし;
        } finally {
            this.保留実行終了種別=実行終了種別.なし;
            Interlocked.Exchange(ref this.停止コマンドゲート,0);
            this.実行コマンド状態変更を通知();
        }
    }

    /// <summary>
    /// 実行停止を待機Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 実行停止を待機Async() {
        // DB ロックなどで 実行Async の終了処理が戻らない場合でも、UI を実行可能状態へ復帰させる。
        var 期限 = DateTimeOffset.UtcNow+TimeSpan.FromSeconds(10);
        while(this.ベンチマーク実行有効か) {
            if(DateTimeOffset.UtcNow>=期限) {
                this.実行状態を強制リセット("ベンチマーク実行が10秒以内に停止しなかったため、UI実行状態を強制リセットします。");
                return;
            }

            await Task.Delay(100);
        }
    }

    /// <summary>
    /// 実行状態を強制リセット メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="理由">理由 を指定して実行条件を制御します。</param>
    private void 実行状態を強制リセット(string 理由) {
        起動診断.書き込む(理由);
        this.実行予測をクリア();
        this.実行キャンセル?.Cancel();
        this.実行キャンセル=null;
        this.保留実行終了種別=実行終了種別.なし;
        Interlocked.Exchange(ref this.実行コマンドゲート,0);
        Interlocked.Exchange(ref this.停止コマンドゲート,0);
        this.実行結果世代=unchecked(this.実行結果世代+1);
        this.実行中か=false;
        this.有効実行ソースタブ=null;
        this.最終ライブ表示クリアを適用(true);
        this.有効実行ルートを設定(null);
        this.OnPropertyChanged(nameof(this.ベンチマーク実行有効か));
        this.OnPropertyChanged(nameof(this.ソート規則編集可能か));
        this.OnPropertyChanged(nameof(this.ツールバー状態入力));
        this.停止コマンド.NotifyCanExecuteChanged();
        this.一時停止コマンド.NotifyCanExecuteChanged();
        this.ツールバーコマンド状態を更新();
    }

    private void 実行コマンド状態変更を通知() {
        this.実行コマンド.NotifyCanExecuteChanged();
        this.実行切替コマンド.NotifyCanExecuteChanged();
        this.DB保存コマンド.NotifyCanExecuteChanged();
        this.新規グリッドコマンド.NotifyCanExecuteChanged();
#if DEBUG
        this.無限実行コマンド.NotifyCanExecuteChanged();
#endif
        this.停止コマンド.NotifyCanExecuteChanged();
        this.一時停止コマンド.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
        this.OnPropertyChanged(nameof(this.DB保存可能か));
        this.OnPropertyChanged(nameof(this.新規グリッド作成可能か));
        this.OnPropertyChanged(nameof(this.ベンチマークを停止可能か));
        this.実行履歴クリアコマンド.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.実行履歴をクリア可能か));
        this.OnPropertyChanged(nameof(this.実行履歴クリアツールチップ文字列));
        this.OnPropertyChanged(nameof(this.ツールバー状態入力));
        this.ツールバーコマンド状態を更新();
    }

    /// <summary>
    /// 有効実行ルートを設定 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="実行ルート">実行ルート を指定して実行条件を制御します。</param>
    private void 有効実行ルートを設定(string? 実行ルート) {
        if(this.有効実行ルート==実行ルート) {
            return;
        }

        this.有効実行ルート=実行ルート;
        this.OnPropertyChanged(nameof(this.ベンチマーク一時停止中か));
        this.OnPropertyChanged(nameof(this.実行ボタン文字列));
        this.OnPropertyChanged(nameof(this.ベンチマークを再開可能か));
        this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
        this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
        this.OnPropertyChanged(nameof(this.ベンチマーク条件を変更可能か));
        this.OnPropertyChanged(nameof(this.実行履歴を選択可能か));
        this.OnPropertyChanged(nameof(this.DB保存可能か));
        this.OnPropertyChanged(nameof(this.新規グリッド作成可能か));
        this.OnPropertyChanged(nameof(this.ベンチマークを停止可能か));
        this.OnPropertyChanged(nameof(this.ツールバー状態入力));
        this.実行コマンド.NotifyCanExecuteChanged();
#if DEBUG
        this.無限実行コマンド.NotifyCanExecuteChanged();
#endif
        this.実行切替コマンド.NotifyCanExecuteChanged();
        this.停止コマンド.NotifyCanExecuteChanged();
        this.一時停止コマンド.NotifyCanExecuteChanged();
        this.ツールバーコマンド状態を更新();
    }

    /// <summary>
    /// 切替計測ミリ秒を整形 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="経過">経過 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string 切替計測ミリ秒を整形(TimeSpan 経過) =>
        経過.TotalMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// 行のジョブか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="ジョブ">ジョブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 行のジョブか(ベンチマーク表行 行,ベンチマークジョブ ジョブ) {
        if(行.プロジェクト別ケース.TryGetValue(ジョブ.プロジェクト,out var ベンチマーク)) {
            return string.Equals(ベンチマーク.メソッド名,ジョブ.メソッド名,StringComparison.OrdinalIgnoreCase)
                &&行の型と一致するか(行,ジョブ.型完全名,ベンチマーク.型完全名)
                &&行の引数と一致するか(行.ソースタブ.タブ,ベンチマーク.引数キー,ジョブ.引数キー);
        }

        return false;
    }

    /// <summary>
    /// 未保存実行ルートを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行プロファイル">実行プロファイル を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string 未保存実行ルートを作成(ベンチマーク実行プロファイル 実行プロファイル) {
        var プロファイルルート = this.ベンチマークルートを取得(実行プロファイル);
        var ルート = Path.Combine(プロファイルルート,".unsaved",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return ルート;
    }

    /// <summary>
    /// 実行または再開Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 実行または再開Async() {
        if(!this.ベンチマークを実行可能か) {
            return;
        }

        if(Interlocked.Exchange(ref this.実行コマンドゲート,1)==1) {
            return;
        }

        try {
            if(this.ベンチマーク一時停止中か&&this.有効実行ルート is not null) {
                await this.実行Async(this.有効実行ルート,true);
                return;
            }
            if(this.選択DB実行の再開準備を試みる(out var 実行ルート)) {
                await this.実行Async(実行ルート,true);
                return;
            }

            await this.実行Async(null,this.現在実行セッション.結果ありか);
        } finally {
            if(!this.ベンチマーク実行有効か) {
                Interlocked.Exchange(ref this.実行コマンドゲート,0);
            }
        }
    }

#if DEBUG
    /// <summary>
    /// 無限実行Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 無限実行Async() {
        if(!this.ベンチマークを実行可能か) {
            return;
        }

        if(Interlocked.Exchange(ref this.実行コマンドゲート,1)==1) {
            return;
        }

        try {
            if(this.ベンチマーク一時停止中か&&this.有効実行ルート is not null) {
                await this.実行Async(this.有効実行ルート,true,true);
                return;
            }

            await this.実行Async(null,true,true);
        } finally {
            if(!this.ベンチマーク実行有効か) {
                Interlocked.Exchange(ref this.実行コマンドゲート,0);
            }
        }
    }
#endif

    /// <summary>
    /// 選択DB実行の再開準備を試みる メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="実行ルート">実行ルート を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool 選択DB実行の再開準備を試みる(out string 実行ルート) {
        実行ルート=string.Empty;
        var 選択肢 = this.選択実行選択肢;
        if(選択肢 is null||選択肢.UI実行ID<=0) {
            return false;
        }

        var 実行 = this.UI実行ストア.実行一覧を取得(UI実行アプリ種別を取得()).FirstOrDefault(項目 => 項目.実行ID==選択肢.UI実行ID);
        if(実行 is null||実行.状態 is not (UI実行状態.一時停止中 or UI実行状態.実行中 or UI実行状態.待機中)) {
            return false;
        }

        var 状態 = this.DB実行から進行状態を作成(選択肢.UI実行ID,実行);
        if(状態 is null||string.IsNullOrWhiteSpace(状態.実行ルート)) {
            return false;
        }

        new ベンチマーク状態マネージャー(this.ベンチマークルートを取得(選択肢.実行プロファイル)).保存(状態);
        this.有効UI実行ID=選択肢.UI実行ID;
        this.有効実行ルートを設定(状態.実行ルート);
        実行ルート=状態.実行ルート;
        return true;
    }

    /// <summary>
    /// 無限テスト実行器を使うか プロパティです。
    /// </summary>
    /// <value>無限テスト実行器を使うか の値を取得/設定します。</value>
    private async Task 実行Async(string? 実行ルート上書き,bool 既存結果を上書きするか,bool 無限テスト実行器を使うか = false) {
        var 一時停止再開か=this.ベンチマーク一時停止中か&&実行ルート上書き is not null;
        if(一時停止再開か) {
            this.実行予測を再開();
        }else{
            this.実行予測を新規開始();
        }
        var 保存DB実行から開始するか = 保存DB実行選択肢か(this.選択実行選択肢);
        if(保存DB実行から開始するか) {
            this.保存DB実行から新規実行用カタログ行を復元();
        }
        this.今回実行ベンチマーク選択スナップショットを更新();
        var 全選択値 = this.現在実行選択ベンチマークケース一覧を取得().ToArray();
        this.未選択結果表示をクリア();
        var 選択値 = 既存結果を上書きするか
            ? 全選択値
            : this.成功結果なしベンチマークケースを抽出(全選択値,this.選択要素数一覧を取得()).ToArray();
        if(選択値.Length==0) {
            this.実行予測をクリア();
            this.状態文字列=全選択値.Length==0
                ? "ベンチマークが選択されていません"
                : "計測対象はすべて成功済みです";
            起動診断.書き込む("ベンチマークが未選択、または選択済みベンチマークがすべて成功済みのため、実行を中止しました。");
            return;
        }

        var 実行プロファイル = 無限テスト実行器を使うか ? ベンチマーク実行プロファイル.テスト : this.選択実行プロファイル.プロファイル;
        var 再開実行か = 実行ルート上書き is not null;
        this.一時実行選択肢を削除();
        if(!再開実行か) {
            this.未保存ベンチマーク結果を破棄();
            this.新規実行用の新規実行選択肢を選択();
            this.最終完了状態をクリア();
        }
        this.選択下段タブ=ベンチマーク下段タブ.CPU;
        this.実行キャンセル?.Cancel();
        var 実行キャンセルソース = new CancellationTokenSource();
        this.実行キャンセル=実行キャンセルソース;
        this.保留実行終了種別=実行終了種別.なし;
        var 実行ルート = 実行ルート上書き??this.未保存実行ルートを作成(実行プロファイル);
        this.現在未保存実行ルート=実行ルート;
        this.有効実行ルートを設定(実行ルート);
        var 実行世代 = unchecked(++this.実行結果世代);
        var 実行ソースタブ = this.選択ソース定義.タブ;
        string? 完了実行ルート = null;
        var 正常完了か = false;
        var 一時停止実行ルートを維持するか = false;
        long? UI実行ID = null;
        // DB 直列保存キューが渋滞していても、押した瞬間に実行状態を可視化し Stop でキャンセルできるようにする。
        // (実行中か/状態文字列 の反映を DB await より後にすると、キュー待ちの間ボタンが「有効なのに無反応」になる)
        this.実行中か=true;
        this.有効実行ソースタブ=実行ソースタブ;
        this.状態文字列="実行準備中";
        try {
            var 初期要素件数一覧 = this.選択要素数一覧を取得();
            var 初期論理CPU一覧 = this.選択論理CPUを取得();
            var 適応閾値 = this.適応閾値を取得();
            UI実行ID=実行ルート上書き is not null&&this.有効UI実行ID is not null
                ? this.有効UI実行ID
                : await this.UI実行を作成Async(実行ソースタブ,実行プロファイル).WaitAsync(実行キャンセルソース.Token);
            if(UI実行ID is { } 実行ID) {
                this.有効UI実行ID=実行ID;
                if(!再開実行か) {
                    this.未保存UI実行ID=実行ID;
                }
            } else {
                起動診断.書き込む("UI DB実行IDなしでベンチマーク実行を開始したため、結果は永続化されません。");
            }
            this.ソースタブ実行中状態をクリア();
            this.今回実行待機表示を適用(選択値);
            this.OnPropertyChanged(nameof(this.ベンチマーク選択を変更できるか));
            if(this.選択結果要素数 is null) {
                this.現在要素数文字列="1";
            }
            this.ソースタブ進行を更新();
            this.現在ベンチマーク選択を取得();
            this.UI実行リポジトリ.実行設定保存を予約(UI実行ID,実行ルート,初期要素件数一覧,適応閾値,初期論理CPU一覧);
            this.UI実行リポジトリ.ベンチマークスナップショット項目保存を予約(UI実行ID,this.ベンチマークグリッドスナップショット項目一覧を作成());
            this.UI実行状態をキューへ追加(UI実行ID,UI実行状態.実行中,"ベンチマーク実行を開始しました。");
            起動診断.書き込む(
                "ベンチマーク実行を要求しました。 "
                +"ソースタブ="+実行ソースタブ+" "
                +"実行プロファイル="+実行プロファイル+" "
                +"無限テスト実行器="+無限テスト実行器を使うか+" "
                +"論理CPU=["+string.Join(",",this.CPU項目一覧.Where(static CPU => CPU.選択中か).Select(static CPU => CPU.論理プロセッサインデックス))+"] "
                +"ベンチマーク="+選択値.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
                +"要素数一覧=["+string.Join(",",this.選択要素数一覧を取得())+"]");
            var レポート = await this.実行サービス.実行Async(
                選択値,
                初期論理CPU一覧,
                this.選択論理CPUを取得,
                初期要素件数一覧,
                実行ソースタブ,
                実行プロファイル,
                適応閾値,
                !再開実行か||既存結果を上書きするか,
                実行ルート,
                this.現在実行選択ベンチマークケース一覧を取得,
                結果 => this.実行結果を受理(結果,実行世代,UI実行ID),
                this.実行状態を受理,
                new ベンチマーク実行オプション(無限テスト実行器を使うか),
                this.実行キャンセル.Token);
            完了実行ルート=レポート.実行ルート;
            this.現在未保存実行ルート=レポート.実行ルート;
            正常完了か=true;
            // 終了直後の DB 保存は直列キューへ投げるだけにし、SQLite のロックなどで UI の状態復帰が
            // ブロックされないようにする（書き込み順序は結果診断保存と同じキューで保証される）。
            this.UI実行状態をキューへ追加(UI実行ID,UI実行状態.完了,"ベンチマーク実行が完了しました。");
            this.状態文字列="計測完了";
            起動診断.書き込む(
                "ベンチマーク実行が完了しました。 "
                +"実行ルート="+レポート.実行ルート);
        } catch(OperationCanceledException) {
            if(this.保留実行終了種別==実行終了種別.一時停止) {
                this.状態文字列="一時停止しました";
                this.UI実行状態をキューへ追加(UI実行ID,UI実行状態.一時停止中,"ベンチマーク実行を一時停止しました。");
                起動診断.書き込む("ベンチマーク実行を一時停止しました。");
                this.有効実行ルートを設定(完了実行ルート??実行ルート);
                一時停止実行ルートを維持するか=true;
            } else {
                this.状態文字列="キャンセルしました";
                this.UI実行状態をキューへ追加(UI実行ID,UI実行状態.停止済み,"ベンチマーク実行を停止しました。");
                起動診断.書き込む("ベンチマーク実行をキャンセルしました。");
                if(ReferenceEquals(this.実行キャンセル,実行キャンセルソース)) {
                    this.有効実行ルートを設定(null);
                }
            }
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマーク実行に失敗しました。",例外);
            this.UI実行状態をキューへ追加(UI実行ID,UI実行状態.失敗,"ベンチマーク実行に失敗しました。 "+例外.Message);
            this.状態文字列="実行失敗 "+例外.Message;
            if(ReferenceEquals(this.実行キャンセル,実行キャンセルソース)) {
                this.有効実行ルートを設定(null);
            }
        } finally {
            // 10秒タイムアウトによる実行状態の強制リセット後に新しい実行が始まっている場合、
            // 遅れて目覚めたこの(旧)実行の finally が新しい実行の状態を上書きしないよう、
            // 実行キャンセル の CTS 参照一致で「自分がまだ現行実行か」を判定してから共有状態を触る。
            var 現在実行か = ReferenceEquals(this.実行キャンセル,実行キャンセルソース);
            if(現在実行か) {
                if(正常完了か) {
                    this.描画スケジューラー.即時反映();
                    await this.実行イベントパイプラインを排出Async();
                } else {
                    this.実行イベントパイプライン.クリア();
                }
            }
            this.永続化バッチ処理.即時反映();
            if(現在実行か) {
                if(一時停止実行ルートを維持するか) {
                    this.実行予測を一時停止();
                }else{
                    this.実行予測を終了();
                }
            }
            if(現在実行か&&!正常完了か&&!一時停止実行ルートを維持するか) {
                this.実行結果世代=unchecked(this.実行結果世代+1);
            }
            if(現在実行か) {
                this.実行中か=false;
                this.有効実行ソースタブ=null;
                this.OnPropertyChanged(nameof(this.ベンチマーク選択を変更できるか));
            }
            // 強制リセットなどで this.実行キャンセル が差し替わっていても、この実行の CTS だけを破棄する。
            実行キャンセルソース.Dispose();
            if(現在実行か) {
                this.実行キャンセル=null;
                this.保留実行終了種別=実行終了種別.なし;
            }
            this.OnPropertyChanged(nameof(this.ベンチマーク実行有効か));
            this.OnPropertyChanged(nameof(this.ソート規則編集可能か));
            this.OnPropertyChanged(nameof(this.ツールバー状態入力));
            this.停止コマンド.NotifyCanExecuteChanged();
            this.一時停止コマンド.NotifyCanExecuteChanged();
            this.ツールバーコマンド状態を更新();
            if(現在実行か) {
                // パイプラインに残った終端イベントの反映完了後に一度だけ表示最終化を行う。
                // クリーンアップより前に立てる(クリーンアップ末尾でも判定するため)。トークンは
                // クリーンアップ実施前の値を控え、実施後(トークンが進んだ後)のみ最終化を許可する。
                this.実行後表示確定保留中か=true;
                this.実行後確定クリア保留トークン=this.最終ライブ表示クリアトークン;
                var 保留実行更新を破棄するか = !正常完了か;
                if(正常完了か&&this.実行イベントパイプライン.保留ありか) {
                    this.最終ライブ表示クリア保留中か=true;
                    this.最終ライブ表示クリア破棄保留中か=保留実行更新を破棄するか;
                    this.保留最終ライブ表示クリアを予約(実行世代);
                } else {
                    this.最終ライブ表示クリアを適用(保留実行更新を破棄するか);
                }
                if(!一時停止実行ルートを維持するか) {
                    this.有効実行ルートを設定(null);
                    if(this.有効UI実行ID==UI実行ID) {
                        this.有効UI実行ID=null;
                    }
                }
            }
            this.選択実行状態キャッシュ=null;
            if(正常完了か) {
                this.未保存実行選択を更新();
            } else {
                this.未保存実行選択を更新();
            }
            this.実行結果強調をクリア();
            if(正常完了か) {
                this.最終ベンチマークソートを適用();
            }
            if(正常完了か) {
                this.ソースタブ進行を同期適用();
                this.最終完了状態を同期適用();
            } else {
                this.ソースタブ進行を更新();
            }
            this.改善プロンプトを更新();
            if(現在実行か&&UI実行ID is { } 最終実行ID) {
                this.UI実行リポジトリ.ベンチマークスナップショット項目保存を予約(最終実行ID,this.ベンチマークグリッドスナップショット項目一覧を作成());
                if(正常完了か) {
                    await this.UI実行リポジトリ.保存完了を待機Async();
                    if(保存DB実行から開始するか) {
                        this.新規実行用の新規実行選択肢を選択(false);
                    } else {
                        await this.DB実行選択肢のみ更新Async(CancellationToken.None);
                    }
                }
            }
        }
    }

    /// <summary>
    /// UI実行状態をキューへ追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="実行ID">実行ID を指定して実行条件を制御します。</param>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="メッセージ">メッセージ を指定して実行条件を制御します。</param>
    private void UI実行状態をキューへ追加(long? 実行ID,UI実行状態 状態,string メッセージ) {
        this.UI実行リポジトリ.実行状態設定を予約(実行ID,状態,メッセージ);
    }

    private void 即時実行中結果をキューへ追加(ベンチマーク結果項目 結果,int 実行世代) {
        this.UIディスパッチャー.呼び出し開始(() => {
            if(実行世代!=this.実行結果世代) {
                return;
            }

            this.即時実行中結果を適用(結果);
        });
    }

    /// <summary>
    /// 実行状態を受理 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    private void 実行状態を受理(string 状態) {
        lock(this.実行状態ゲート) {
            this.保留実行状態文字列=状態;
        }
        this.実行状態更新を要求();
    }

    /// <summary>
    /// 実行状態更新を要求 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 実行状態更新を要求() {
        if(Interlocked.Exchange(ref this.実行状態更新予約済みか,1)==1) {
            return;
        }

        _=Task.Run(async () => {
            await Task.Delay(ベンチマークパイプライン定数.UI描画間隔ミリ秒);
            this.UIディスパッチャー.呼び出し開始(() => {
                string 文字列;
                lock(this.実行状態ゲート) {
                    文字列=this.保留実行状態文字列;
                    this.保留実行状態文字列=string.Empty;
                }
                if(!string.IsNullOrWhiteSpace(文字列)) {
                    this.状態文字列=文字列;
                }
                Interlocked.Exchange(ref this.実行状態更新予約済みか,0);
                lock(this.実行状態ゲート) {
                    if(string.IsNullOrWhiteSpace(this.保留実行状態文字列)) {
                        return;
                    }
                }
                this.実行状態更新を要求();
            });
        });
    }

    /// <summary>
    /// 即時実行中結果を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    private void 即時実行中結果を適用(ベンチマーク結果項目 結果) {
        if(!this.ベンチマーク実行有効か) {
            return;
        }

        起動診断.パンくずを追加(this.結果パンくずを作成("即時実行中結果",結果));
        if(結果.軸数>0) {
            this.現在実行結果項目を追加または更新(結果);
            this.実行中結果用ソースタブ進行を更新();
        }

        if(結果.ソースタブ!=this.選択ソース定義.タブ) {
            return;
        }

        this.表示実行中結果をグリッドセルへ適用(
            結果,
            this.割当CPUを検索(結果),
            DateTimeOffset.Now,
            自動スクロールを要求するか: true,
            完了強調を維持するか: true);
    }

    /// <summary>
    /// 実行中結果か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 実行中結果か(ベンチマーク結果項目 結果) =>
        ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.実行中);

    /// <summary>
    /// ツールバーコマンド状態を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ツールバーコマンド状態を更新() {
        this.OnPropertyChanged(nameof(this.ツールバー状態入力));
        this.実行コマンド.NotifyCanExecuteChanged();
        this.DB保存コマンド.NotifyCanExecuteChanged();
        this.新規グリッドコマンド.NotifyCanExecuteChanged();
        this.停止コマンド.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
        this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
        this.OnPropertyChanged(nameof(this.実行履歴を選択可能か));
        this.OnPropertyChanged(nameof(this.DB保存可能か));
        this.OnPropertyChanged(nameof(this.新規グリッド作成可能か));
        this.実行履歴クリアコマンド.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.実行履歴をクリア可能か));
    }

    /// <summary>
    /// 終了ジョブか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ジョブ">ジョブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 終了ジョブか(ベンチマークジョブ ジョブ) =>
        ジョブ.状態 is ジョブ状態.完了 or ジョブ状態.失敗;

    /// <summary>
    /// スキップセルを適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="プロジェクト">プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="停止済み日時要素数">停止済み日時要素数 を指定して実行条件を制御します。</param>
    private void スキップセルを適用(IReadOnlyList<ベンチマーク結果項目> 結果一覧,ベンチマーク表行 行,string プロジェクト,int 停止済み日時要素数) {
        var 型完全名 = 行の型完全名を取得(行,プロジェクト);
        var 引数キー = 行の引数キーを取得(行,プロジェクト);
        var 結果 = this.結果項目を取得(結果一覧,行.メソッド名,プロジェクト,型完全名,停止済み日時要素数,引数キー);
        var CPU = 結果?.割り当て論理CPU is { } 論理CPU
            ? this.割当CPUを検索(結果)
            : null;
        var 測定済み日時 = 結果?.開始日時;
        var メタデータ欠測か = 結果 is null||CPU is null||測定済み日時 is null;
        if(メタデータ欠測か) {
            this.ベンチマークメタデータ欠測を報告(行.メソッド名,プロジェクト,停止済み日時要素数,結果);
        }

        行.スキップを適用(
            プロジェクト,
            停止済み日時要素数,
            this.結果測定値を取得(結果一覧,行.メソッド名,プロジェクト,型完全名,停止済み日時要素数,引数キー),
            CPU,
            測定済み日時,
            メタデータ欠測か);
    }

    private static ジョブ状態 再開用ジョブ状態へ変換(UI実行状態 状態) =>
        状態 switch {
            UI実行状態.完了 => ジョブ状態.完了,
            UI実行状態.失敗 => ジョブ状態.失敗,
            _ => ジョブ状態.保留,
        };
}
