using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Seamless.UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

internal sealed class ベンチマークデータグリッド更新バッチイベント引数:EventArgs {
    public static readonly ベンチマークデータグリッド更新バッチイベント引数 開始 = new(true);
    public static readonly ベンチマークデータグリッド更新バッチイベント引数 終了 = new(false);
    private ベンチマークデータグリッド更新バッチイベント引数(bool 開始時か) => this.開始時か=開始時か;
    public bool 開始時か { get; }
}

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
    /// ベンチマークソート設定キー フィールドです。
    /// </summary>
    private const string ベンチマークソート設定キー = "Seamless.SortRules.v1";
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
    /// カタログサービス フィールドです。
    /// </summary>
    private readonly Iベンチマークカタログサービス カタログサービス;
    /// <summary>
    /// CPUトポロジーサービス フィールドです。
    /// </summary>
    private readonly ICPUトポロジーサービス CPUトポロジーサービス;
    /// <summary>
    /// 実行サービス フィールドです。
    /// </summary>
    private readonly Iベンチマーク実行サービス 実行サービス;
    /// <summary>
    /// 比較サービス フィールドです。
    /// </summary>
    private readonly Iベンチマーク比較サービス 比較サービス;
    /// <summary>
    /// UI実行ストア フィールドです。
    /// </summary>
    private readonly UI実行ストア UI実行ストア;
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
    /// 有効実行ルート フィールドです。
    /// </summary>
    private string? 有効実行ルート;
    /// <summary>
    /// 保留実行終了種別 フィールドです。
    /// </summary>
    private 実行終了種別 保留実行終了種別;
    private int 実行コマンドゲート;
    private int 停止コマンドゲート;
    /// <summary>
    /// 状態文字列値 フィールドです。
    /// </summary>
    private string 状態文字列値 = "準備完了";
    /// <summary>
    /// ベンチマーク実行中か値 フィールドです。
    /// </summary>
    private bool 内部ベンチマーク実行中か;
    /// <summary>
    /// 更新中か値 フィールドです。
    /// </summary>
    private bool 内部更新中か;
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
    /// ソースタブ表示状態一覧値 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークソースタブ表示状態> ソースタブ表示状態一覧値 = [];
    /// <summary>
    /// タブ別ソースタブ表示状態 フィールドです。
    /// </summary>
    private readonly Dictionary<ベンチマークソースタブ,ベンチマークソースタブ表示状態> タブ別ソースタブ表示状態 = [];
    /// <summary>
    /// 選択実行プロファイル値 フィールドです。
    /// </summary>
    private 実行プロファイル選択肢 選択実行プロファイル値;
    /// <summary>
    /// 選択閾値値 フィールドです。
    /// </summary>
    private 閾値選択肢 選択閾値値;
    /// <summary>
    /// 選択実行選択肢値 フィールドです。
    /// </summary>
    private ベンチマーク実行選択肢? 選択実行選択肢値;
    /// <summary>
    /// 選択結果要素数値 フィールドです。
    /// </summary>
    private 結果要素数選択肢? 選択結果要素数値;
    /// <summary>
    /// 現在要素数文字列値 フィールドです。
    /// </summary>
    private string 現在要素数文字列値 = "1";
    /// <summary>
    /// 改善プロンプト文字列値 フィールドです。
    /// </summary>
    private string 改善プロンプト文字列値 = string.Empty;
    /// <summary>
    /// 改善候補概要文字列値 フィールドです。
    /// </summary>
    private string 改善候補概要文字列値 = string.Empty;
    private string 全体ベンチマークシェア概要文値 = ベンチマーク改善プロンプトビルダー.概要を作成([]).全体表示文字列;
    private string 全体ベンチマークシェア概要短文 = ベンチマーク改善プロンプトビルダー.概要を作成([]).短縮表示文字列;
    /// <summary>
    /// CPU選択適用中か フィールドです。
    /// </summary>
    private bool CPU選択適用中か;
    /// <summary>
    /// UI状態適用中か フィールドです。
    /// </summary>
    private bool UI状態適用中か;
    /// <summary>
    /// ベンチマーク選択適用中か フィールドです。
    /// </summary>
    private bool ベンチマーク選択適用中か;
    /// <summary>
    /// 結果要素数適用中か フィールドです。
    /// </summary>
    private bool 結果要素数適用中か;
    /// <summary>
    /// 実行選択肢選択適用中か フィールドです。
    /// </summary>
    private bool 実行選択肢選択適用中か;
    /// <summary>
    /// ソースタブ選択適用中か フィールドです。
    /// </summary>
    private bool ソースタブ選択適用中か;
    /// <summary>
    /// ソート規則適用中か フィールドです。
    /// </summary>
    private bool ソート規則適用中か;
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
    /// ベンチマーク選択表示更新中か フィールドです。
    /// </summary>
    private bool ベンチマーク選択表示更新中か;
    /// <summary>
    /// 終了状態保存保留ありか値 フィールドです。
    /// </summary>
    private bool 内部終了状態保存保留ありか;
    /// <summary>
    /// 未保存ベンチマーク結果ありか値 フィールドです。
    /// </summary>
    private bool 内部未保存ベンチマーク結果ありか;
    /// 現在ソート列値 フィールドです。
    /// </summary>
    private ベンチマークグリッドソート列 現在ソート列値 = ベンチマークグリッドソート列.ベンチマークメソッド;
    /// <summary>
    /// 現在ソート降順か値 フィールドです。
    /// </summary>
    private bool 内部現在ソート降順か;
    /// <summary>
    /// 比較行一覧値 プロパティです。
    /// </summary>
    /// <value>比較行一覧値 の値を取得/設定します。</value>
    private IReadOnlyList<ベンチマーク比較行> 比較行一覧値 {
        get => this.選択ソースタブ表示状態.比較行一覧;
        set => this.選択ソースタブ表示状態.比較行一覧=value;
    }
    /// <summary>
    /// 現在実行セッション フィールドです。
    /// </summary>
    private readonly ベンチマーク実行セッション 現在実行セッション = new();
    private IReadOnlyList<ベンチマーク結果項目> 直近完了結果項目一覧 = [];
    /// <summary>
    /// ベンチマーク行選択状態 フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> ベンチマーク行選択状態 = [with(StringComparer.OrdinalIgnoreCase)];
    /// <summary>
    /// 改善候補選択状態 フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> 改善候補選択状態 = [with(StringComparer.OrdinalIgnoreCase)];
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
    /// ソースタブ実行中状態 フィールドです。
    /// </summary>
    private readonly ベンチマークソースタブ実行中状態 ソースタブ実行中状態 = new();
    /// <summary>
    /// カタログケース一覧 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークケース> カタログケース一覧 = [];
    /// <summary>
    /// 保存ソート規則スナップショット一覧 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークソート規則スナップショット> 保存ソート規則スナップショット一覧 = [];
    /// <summary>
    /// 有効実行ソースタブ値 フィールドです。
    /// </summary>
    private ベンチマークソースタブ? 有効実行ソースタブ値;
    /// <summary>
    /// 現在実行選択ベンチマークケース一覧 フィールドです。
    /// </summary>
    private IReadOnlyList<ベンチマークケース> 現在実行選択ベンチマークケース一覧 = [];
    /// <summary>
    /// 実行開始時に固定する、タブ別の選択済み進行セルです。
    /// </summary>
    private IReadOnlyDictionary<ベンチマークソースタブ,IReadOnlyList<ベンチマークタブ進行セル>> 現在実行タブ別進行セル一覧 = new Dictionary<ベンチマークソースタブ,IReadOnlyList<ベンチマークタブ進行セル>>();
    /// <summary>
    /// 選択論理CPU一覧 フィールドです。
    /// </summary>
    private int[] 選択論理CPU一覧 = [];
    /// <summary>
    /// ログ文字列値 フィールドです。
    /// </summary>
    private string ログ文字列値 = string.Empty;
    /// <summary>
    /// 選択下段タブ値 フィールドです。
    /// </summary>
    private ベンチマーク下段タブ 選択下段タブ値 = ベンチマーク下段タブ.CPU;
    /// <summary>
    /// 全体ソースタブ進行値 フィールドです。
    /// </summary>
    private ベンチマークタブ進行 全体ソースタブ進行値;
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
    /// 実行状態ゲート フィールドです。
    /// </summary>
    private readonly object 実行状態ゲート = new();
    /// <summary>
    /// 保留実行状態文字列 フィールドです。
    /// </summary>
    private string 保留実行状態文字列 = string.Empty;
    /// <summary>
    /// 実行結果世代 フィールドです。
    /// </summary>
    private int 実行結果世代;
    /// <summary>
    /// 最終クリーンアップ後に発火する古いジョブ単位の遅延適用を無効化するためのトークンです。
    /// </summary>
    private int 最終ライブ表示クリアトークン;
    /// <summary>
    /// 実行終了後、パイプライン排出完了時に一度だけ表示最終化を行うためのフラグです。
    /// </summary>
    private bool 実行後表示確定保留中か;
    /// <summary>
    /// 表示最終化を最終クリーンアップ実施後まで遅らせるための、フラグ設定時点のクリーンアップトークンです。
    /// </summary>
    private int 実行後確定クリア保留トークン;
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
    /// 実行状態更新予約済みか フィールドです。
    /// </summary>
    private int 実行状態更新予約済みか;
    /// <summary>
    /// 現在未保存実行ルート フィールドです。
    /// </summary>
    private string? 現在未保存実行ルート;
    /// <summary>
    /// 有効UI実行ID フィールドです。
    /// </summary>
    private long? 有効UI実行ID;
    /// <summary>
    /// 未保存UI実行ID フィールドです。
    /// </summary>
    private long? 未保存UI実行ID;
    /// <summary>
    /// 保留ソースタブ選択肢 フィールドです。
    /// </summary>
    private ソースタブ選択肢? 保留ソースタブ選択肢;
    // 選択中の保存済み実行の進行状態スナップショット。
    // タブ切替時にDBを再読み込みしないため、実行選択の変更と実行終了時に破棄する。
    private (long UI実行ID,進行状態? 状態)? 選択実行状態キャッシュ;
    /// <summary>
    /// 読込済み改善選択実行ID フィールドです。
    /// </summary>
    private long? 読込済み改善選択実行ID;
    /// <summary>
    /// 改善候補選択読込済みか フィールドです。
    /// </summary>
    private bool 改善候補選択読込済みか;
    /// <summary>
    /// 改善候補選択適用中か フィールドです。
    /// </summary>
    private bool 改善候補選択適用中か;

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
        new("中央",this.選択ソース定義.中央見出し,["中央"]),
        new("追加",this.選択ソース定義.追加見出し,["追加"]),
        new("右",this.選択ソース定義.右見出し,["右"]),
    ];

    /// <summary>
    /// ベンチマークプロジェクト見出し文字列を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークプロジェクト見出し文字列を更新() {
        foreach(var グループ in this.ベンチマークプロジェクト選択グループ一覧) {
            if(グループ.ID=="左") {
                グループ.見出し文字列を設定(this.選択ソース定義.左見出し);
            } else if(グループ.ID=="中央") {
                グループ.見出し文字列を設定(this.選択ソース定義.中央見出し);
            } else if(グループ.ID=="追加") {
                グループ.見出し文字列を設定(this.選択ソース定義.追加見出し);
            } else if(グループ.ID=="右") {
                グループ.見出し文字列を設定(this.選択ソース定義.右見出し);
            }
        }
    }

    /// <summary>
    /// ソースタブ表示状態一覧 プロパティです。
    /// </summary>
    /// <value>ソースタブ表示状態一覧 の値を取得/設定します。</value>
    public IReadOnlyList<ベンチマークソースタブ表示状態> ソースタブ表示状態一覧 => this.ソースタブ表示状態一覧値;
    /// <summary>
    /// 選択ソースタブ表示状態 プロパティです。
    /// </summary>
    /// <value>選択ソースタブ表示状態 の値を取得/設定します。</value>
    public ベンチマークソースタブ表示状態 選択ソースタブ表示状態 => this.ソースタブ表示状態を取得(this.選択ソース定義.タブ);
    /// <summary>
    /// ベンチマーク行一覧 プロパティです。
    /// </summary>
    /// <value>ベンチマーク行一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク表行> ベンチマーク行一覧 => this.選択ソースタブ表示状態.行一覧;
    /// <summary>
    /// 結果項目一覧 プロパティです。
    /// </summary>
    /// <value>結果項目一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク結果項目> 結果項目一覧 { get; } = [];
    /// <summary>
    /// 表示結果項目一覧 プロパティです。
    /// </summary>
    /// <value>表示結果項目一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク結果項目> 表示結果項目一覧 { get; } = [];
    /// <summary>
    /// CPU項目一覧 プロパティです。
    /// </summary>
    /// <value>CPU項目一覧 の値を取得/設定します。</value>
    public ObservableCollection<CPU選択項目> CPU項目一覧 { get; } = [];
    /// <summary>
    /// CPU区分一覧 プロパティです。
    /// </summary>
    /// <value>CPU区分一覧 の値を取得/設定します。</value>
    public ObservableCollection<CPU表区分> CPU区分一覧 { get; } = [];
    /// <summary>
    /// 改善候補一覧 プロパティです。
    /// </summary>
    /// <value>改善候補一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク改善候補行> 改善候補一覧 { get; } = [];
    /// <summary>
    /// 高性能CPU区分一覧 プロパティです。
    /// </summary>
    /// <value>高性能CPU区分一覧 の値を取得/設定します。</value>
    public IEnumerable<CPU表区分> 高性能CPU区分一覧 => this.CPU区分を取得(CPUコア種別.高性能);
    /// <summary>
    /// 高効率CPU区分一覧 プロパティです。
    /// </summary>
    /// <value>高効率CPU区分一覧 の値を取得/設定します。</value>
    public IEnumerable<CPU表区分> 高効率CPU区分一覧 => this.CPU区分を取得(CPUコア種別.高効率);
    /// <summary>
    /// 不明CPU区分一覧 プロパティです。
    /// </summary>
    /// <value>不明CPU区分一覧 の値を取得/設定します。</value>
    public IEnumerable<CPU表区分> 不明CPU区分一覧 => this.CPU区分を取得(CPUコア種別.不明);
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
    /// 結果要素数選択肢一覧 プロパティです。
    /// </summary>
    /// <value>結果要素数選択肢一覧 の値を取得/設定します。</value>
    public ObservableCollection<結果要素数選択肢> 結果要素数選択肢一覧 { get; } = [];
    /// <summary>
    /// 実行選択肢一覧 プロパティです。
    /// </summary>
    /// <value>実行選択肢一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク実行選択肢> 実行選択肢一覧 { get; } = [];
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
    /// <summary>
    /// 状態文字列 プロパティです。
    /// </summary>
    /// <value>状態文字列 の値を取得/設定します。</value>
    public string 状態文字列 {
        get => this.状態文字列値;
        set => this.SetProperty(ref this.状態文字列値,value);
    }

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

    /// <summary>
    /// ベンチマークシェア概要文プロパティです。
    /// </summary>
    /// <value>ベンチマークシェア概要文の値を取得/設定します。</value>
    public string ベンチマークシェア概要文 {
        get => this.選択ソースタブ表示状態.ベンチマークシェア概要文;
        /// <summary>
        /// set プロパティを取得/設定します。
        /// </summary>
        /// <value>set の値です。</value>
        private set {
            if(this.選択ソースタブ表示状態.ベンチマークシェア概要文!=value) {
                this.選択ソースタブ表示状態.ベンチマークシェア概要文=value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 改善プロンプト文字列 プロパティです。
    /// </summary>
    /// <value>改善プロンプト文字列 の値を取得/設定します。</value>
    public string 改善プロンプト文字列 {
        get => this.改善プロンプト文字列値;
        set {
            if(this.SetProperty(ref this.改善プロンプト文字列値,value)) {
                this.OnPropertyChanged(nameof(this.改善プロンプトをコピー可能か));
                this.改善プロンプトコピーコマンド.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// 改善候補概要文字列 プロパティです。
    /// </summary>
    /// <value>改善候補概要文字列 の値を取得/設定します。</value>
    public string 改善候補概要文字列 {
        get => this.改善候補概要文字列値;
        private set => this.SetProperty(ref this.改善候補概要文字列値,value);
    }

    /// <summary>
    /// 改善プロンプトをコピー可能か プロパティです。
    /// </summary>
    /// <value>改善プロンプトをコピー可能か の値を取得/設定します。</value>
    public bool 改善プロンプトをコピー可能か => !string.IsNullOrWhiteSpace(this.改善プロンプト文字列);

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
    /// ベンチマーク選択を変更できるか プロパティです。
    /// </summary>
    /// <value>ベンチマーク選択を変更できるか の値を取得/設定します。</value>
    public bool ベンチマーク選択を変更できるか =>
        !this.更新中か;

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

    // 実行中は行位置を固定するため、ソートルールの編集(追加/削除/移動/方向切替)をUIから禁止する。
    /// <summary>
    /// ソート規則編集可能か プロパティです。
    /// </summary>
    /// <value>ソート規則編集可能か の値を取得/設定します。</value>
    public bool ソート規則編集可能か =>
        !this.ベンチマーク一時停止中か
        &&(!this.ベンチマーク実行有効か||this.選択ソース定義.タブ!=this.有効実行ソースタブ);

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
    /// 実行ツールバーボタン使用可能か プロパティです。
    /// </summary>
    /// <value>実行ツールバーボタン使用可能か の値を取得/設定します。</value>
    public bool 実行ツールバーボタン使用可能か => this.ベンチマークを実行可能か||this.ベンチマークを再開可能か;

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
    /// 実行ボタンツールチップ文字列 プロパティです。
    /// </summary>
    /// <value>実行ボタンツールチップ文字列 の値を取得/設定します。</value>
    public string 実行ボタンツールチップ文字列 =>
        this.選択ベンチマークセルありか||保存DB実行選択肢か(this.選択実行選択肢)
            ? this.実行ボタン文字列
            : "ベンチマークを1つ以上選択してください";

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
    /// 全ベンチマーク選択状態 プロパティです。
    /// </summary>
    /// <value>全ベンチマーク選択状態 の値を取得/設定します。</value>
    public bool? 全ベンチマーク選択状態 {
        get => this.全ベンチマーク選択状態値;
        private set => this.SetProperty(ref this.全ベンチマーク選択状態値,value);
    }

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
    /// 選択下段タブ プロパティです。
    /// </summary>
    /// <value>選択下段タブ の値を取得/設定します。</value>
    public ベンチマーク下段タブ 選択下段タブ {
        get => this.選択下段タブ値;
        set {
            if(this.SetProperty(ref this.選択下段タブ値,value)) {
                this.OnPropertyChanged(nameof(this.ログ下段タブ表示中か));
                this.OnPropertyChanged(nameof(this.改善プロンプト下段タブ表示中か));
            }
        }
    }

    /// <summary>
    /// ログ下段タブ表示中か プロパティです。
    /// </summary>
    /// <value>ログ下段タブ表示中か の値を取得/設定します。</value>
    public bool ログ下段タブ表示中か => this.選択下段タブ==ベンチマーク下段タブ.ログ;
    /// <summary>
    /// 改善プロンプト下段タブ表示中か プロパティです。
    /// </summary>
    /// <value>改善プロンプト下段タブ表示中か の値を取得/設定します。</value>
    public bool 改善プロンプト下段タブ表示中か => this.選択下段タブ==ベンチマーク下段タブ.改善プロンプト;

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
                _=this.選択プロファイルの実行一覧を更新Async();
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

    /// <summary>
    /// 現在要素数文字列 プロパティです。
    /// </summary>
    /// <value>現在要素数文字列 の値を取得/設定します。</value>
    public string 現在要素数文字列 {
        get => this.現在要素数文字列値;
        private set => this.SetProperty(ref this.現在要素数文字列値,value);
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

    private ソースタブ選択肢 ソースタブ選択肢を取得(ベンチマークソースタブ ソースタブ) =>
        this.ソースタブ一覧.FirstOrDefault(選択肢 => 選択肢.定義.タブ==ソースタブ)??this.ソースタブ一覧[0];

    /// <summary>
    /// 全ベンチマーク行を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<ベンチマーク表行> 全ベンチマーク行を取得() =>
        this.ソースタブ表示状態一覧値.SelectMany(static 状態 => 状態.行一覧);

    /// <summary>
    /// 初期化Async メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task 初期化Async() {
        await this.更新Async();
    }

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
    /// CPUを切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="CPU">CPU を指定して実行条件を制御します。</param>
    public void CPUを切り替え(CPU選択項目 CPU) {
        if(this.更新中か) {
            return;
        }

        CPU.選択中か=!CPU.選択中か;
    }

    internal void CPUチェックを切り替え(IReadOnlyList<CPU選択項目> CPU一覧) {
        if(this.更新中か||CPU一覧.Count==0) {
            return;
        }

        var 対象一覧 = CPU一覧.Distinct().ToArray();
        var 選択値 = グリッドチェック切替ロジック.目標チェック状態を解決(
            対象一覧.Select(static CPU => (bool?)CPU.選択中か));
        if(選択値 is { } 値か) {
            this.CPU選択を適用(対象一覧,値か);
        }
    }

    /// <summary>
    /// CPU区分を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="区分">区分 を指定して実行条件を制御します。</param>
    public void CPU区分を切り替え(CPU表区分 区分) {
        if(this.更新中か) {
            return;
        }

        var 選択中か = 区分.状態!=CPU選択状態.チェック済み;
        this.CPU選択を適用(区分.セル一覧.Select(static セル => セル.CPU),選択中か);
    }

    /// <summary>
    /// CPU列を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="区分">区分 を指定して実行条件を制御します。</param>
    /// <param name="列インデックス">列インデックス を指定して実行条件を制御します。</param>
    public void CPU列を切り替え(CPU表区分 区分,int 列インデックス) {
        if(this.更新中か) {
            return;
        }

        var セル一覧 = 区分.行一覧
            .Where(行 => 列インデックス<行.セル一覧.Count)
            .Select(行 => 行.セル一覧[列インデックス])
            .ToArray();
        var 選択中か = 区分.列状態を取得(列インデックス)!=CPU選択状態.チェック済み;
        this.CPU選択を適用(セル一覧.Select(static セル => セル.CPU),選択中か);
    }

    /// <summary>
    /// CPU行を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    public void CPU行を切り替え(CPU表行 行) {
        if(this.更新中か) {
            return;
        }

        var 選択中か = 行.状態!=CPU選択状態.チェック済み;
        this.CPU選択を適用(行.セル一覧.Select(static セル => セル.CPU),選択中か);
    }

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

    public string 全体ベンチマークシェア概要文 {
        get => this.全体ベンチマークシェア概要文値;
        private set {
            if(this.SetProperty(ref this.全体ベンチマークシェア概要文値,value)) {
                this.OnPropertyChanged(nameof(this.全体ソース進行概要文字列));
            }
        }
    }

    public string 全体ソース進行概要文字列 =>
        "確定 "+(this.全体ソースタブ進行.合計数<=0 ? "0/0" : this.全体ソースタブ進行.割合文字列)
        +","+this.全体ベンチマークシェア概要短文;

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
    /// 切替計測ミリ秒を整形 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="経過">経過 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string 切替計測ミリ秒を整形(TimeSpan 経過) =>
        経過.TotalMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);

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

    private void 新規選択用に復元実行再開状態をクリア() {
        this.有効UI実行ID=null;
        this.現在未保存実行ルート=null;
        this.有効実行ルートを設定(null);
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

    private static IEnumerable<UIベンチマークグリッド側記録> スナップショット側一覧を取得(UIベンチマークスナップショット項目記録 項目) {
        yield return 項目.左;
        if(項目.中央 is { } 中央) {
            yield return 中央;
        }
        if(項目.追加 is { } 追加) {
            yield return 追加;
        }
        yield return 項目.右;
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
    /// 今回実行ベンチマーク選択スナップショットを更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 今回実行ベンチマーク選択スナップショットを更新() {
        var 選択ケース一覧=this.全タブから選択ベンチマークケースを作成();
        var タブ別進行セル一覧=this.ソースタブ一覧.ToDictionary(
            static 選択肢=>選択肢.定義.タブ,
            選択肢=>(IReadOnlyList<ベンチマークタブ進行セル>)ベンチマークタブ進行元ロジック.選択ケースからセルを作成(選択ケース一覧,選択肢.定義));
        Volatile.Write(ref this.現在実行選択ベンチマークケース一覧,選択ケース一覧);
        Volatile.Write(ref this.現在実行タブ別進行セル一覧,タブ別進行セル一覧);
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

    private IReadOnlyList<ベンチマークグリッドスナップショット項目> ベンチマークグリッドスナップショット項目一覧を作成() {
        var 結果一覧 = this.全グリッド結果項目を取得();
        var 項目一覧 = new List<ベンチマークグリッドスナップショット項目>();
        var 順序 = 0;
        foreach(var 行 in this.全ベンチマーク行を取得()) {
            if(this.ベンチマークグリッドスナップショット項目を作成(行,順序,結果一覧) is { } 項目) {
                項目一覧.Add(項目);
                順序++;
            }
        }

        return 項目一覧;
    }

    private ベンチマークグリッドスナップショット項目? ベンチマークグリッドスナップショット項目を作成(ベンチマーク表行 行,int 順序,IReadOnlyList<ベンチマーク結果項目> 結果一覧) {
        var 列値一覧=行.ソースタブ.列一覧.Select(列=>{
            行.プロジェクト別ケース.TryGetValue(列.プロジェクト,out var ベンチマーク);
            var セル=行.プロジェクトセルを取得(列.プロジェクト)!;
            var 結果=ベンチマーク is null ? null : this.スナップショット結果を検索(行,ベンチマーク,結果一覧);
            var CPU=結果?.割り当て論理CPU??CPU番号を解析(セル.論理プロセッサ文字列);
            var 側=this.ベンチマークグリッドスナップショット側を作成(行,セル,ベンチマーク,結果);
            return new { ベンチマーク,CPU,側 };
        }).ToArray();
        if(列値一覧.All(static 値=>値.ベンチマーク is null)) {
            return null;
        }

        var CPU一覧=列値一覧.Select(static 値=>値.CPU).Where(static CPU=>CPU.HasValue).Select(static CPU=>CPU!.Value).Distinct().ToArray();
        if(CPU一覧.Length>1) {
            起動診断.書き込む("ベンチマークスナップショットのCPUが一致しません。実行スナップショットには行共通CPUを保存します。"
                +" ソースタブ="+行.ソースタブ.ID+" 型="+行.ベンチマーク型文字列+" ベンチマーク="+行.メソッド名+" CPU="+string.Join(",",CPU一覧));
        }

        var 左=列値一覧[0];
        var 右=列値一覧[^1];
        var 中央=列値一覧.Length>2 ? 列値一覧[1] : null;
        var 追加=列値一覧.Length>3 ? 列値一覧[2] : null;
        var 型名=スナップショット型名を解決(行,左.ベンチマーク,中央?.ベンチマーク,右.ベンチマーク);
        if(string.IsNullOrWhiteSpace(型名)) {
            起動診断.書き込む("ベンチマークスナップショットの型表示が空です。 ソースタブ="+行.ソースタブ.ID+" ベンチマーク="+行.メソッド名);
        }

        return new ベンチマークグリッドスナップショット項目(
            0,順序,行.ソースタブ.ID,型名,行.メソッド名,列値一覧.Select(static 値=>値.CPU).FirstOrDefault(static CPU=>CPU.HasValue),行.選択中か,
            左.側,右.側,中央?.側,追加?.側);
    }
    private static string スナップショット型名を解決(
        ベンチマーク表行 行,
        ベンチマークケース? 左ベンチマーク,
        ベンチマークケース? 中央ベンチマーク,
        ベンチマークケース? 右ベンチマーク) {
        // ライブ計測時の表示(型.BaseTypeから整形した完全なパス、例:"Tsavorite<ClassInt32>")を
        // そのまま型名として保存しておくと、ロード後も同じ表示形式を再現できる。
        // スケーラブルタブは 前回結果を検索 が型名を短いトークンとして比較するため対象外とする。
        if(!スケーラブルベンチマーク名フォーマッター.スケーラブルソースタブか(行.ソースタブ.タブ)
            &&!string.IsNullOrWhiteSpace(行.表示用ベンチマークパス)) {
            return 行.表示用ベンチマークパス;
        }
        if(!string.IsNullOrWhiteSpace(行.ベンチマーク型文字列)) {
            return 行.ベンチマーク型文字列;
        }

        foreach(var ベンチマーク in new[]{左ベンチマーク,中央ベンチマーク,右ベンチマーク}) {
            if(ベンチマーク is null||string.IsNullOrWhiteSpace(ベンチマーク.型完全名)) {
                continue;
            }

            var 型名 = スケーラブルベンチマーク名フォーマッター.ジェネリック型表示を作成(ベンチマーク.型完全名);
            if(!string.IsNullOrWhiteSpace(型名)) {
                return 型名;
            }

            var 短名 = スケーラブルベンチマーク名フォーマッター.型短名(ベンチマーク.型完全名);
            if(!string.IsNullOrWhiteSpace(短名)) {
                return 短名;
            }
        }

        return string.Empty;
    }

    private ベンチマークグリッドスナップショット側 ベンチマークグリッドスナップショット側を作成(
        ベンチマーク表行 行,
        ベンチマークプロジェクトセル セル,
        ベンチマークケース? ベンチマーク,
        ベンチマーク結果項目? 結果) {
        var 状態 = this.スナップショット状態を解決(セル,結果);
        var 測定済み日時 = 結果?.開始日時??セル.最終測定日時??セル.最終失敗日時;
        var 要素数 = 結果 is { 要素数:>0 } ? 結果.要素数 : セル.最終成功要素数??セル.最終失敗要素数;
        var 平均ナノ秒 = 結果?.平均ナノ秒??セル.測定値.時間ナノ秒;
        var 割り当てバイト数 = 結果?.割り当てバイト数値??セル.測定値.割り当てバイト数;
        var 結果項目 = 結果??スナップショット結果項目を作成(
            行.ソースタブ,
            セル.プロジェクト,
            ベンチマーク?.アセンブリ名??行.ソースタブ.アセンブリ名を取得(セル.プロジェクト),
            ベンチマーク?.型完全名??string.Empty,
            ベンチマーク?.メソッド名??行.メソッド名,
            ベンチマーク?.引数キー??string.Empty,
            ベンチマーク?.引数一覧?? [],
            状態,
            要素数,
            CPU番号を解析(セル.論理プロセッサ文字列),
            測定済み日時,
            平均ナノ秒,
            割り当てバイト数,
            結果?.失敗理由??string.Empty);
        return new(
            状態,
            結果?.失敗理由??string.Empty,
            測定済み日時,
            平均ナノ秒,
            割り当てバイト数,
            結果項目,
            ベンチマーク);
    }

    private ベンチマークグリッドスナップショット側 空ベンチマークグリッドスナップショット側を作成(
        ベンチマーク表行 行,
        ベンチマークプロジェクトセル セル,
        ベンチマークケース? ベンチマーク) {
        var 結果項目 = スナップショット結果項目を作成(
            行.ソースタブ,
            セル.プロジェクト,
            ベンチマーク?.アセンブリ名??行.ソースタブ.アセンブリ名を取得(セル.プロジェクト),
            ベンチマーク?.型完全名??string.Empty,
            ベンチマーク?.メソッド名??行.メソッド名,
            ベンチマーク?.引数キー??string.Empty,
            ベンチマーク?.引数一覧?? [],
            UI実行状態.待機中,
            ベンチマーク要素数一覧.基本要素数,
            null,
            null,
            null,
            null,
            string.Empty);
        return new(UI実行状態.待機中,string.Empty,null,null,null,結果項目,ベンチマーク);
    }

    private ベンチマーク結果項目? スナップショット結果を検索(ベンチマーク表行 行,ベンチマークケース ベンチマーク,IReadOnlyList<ベンチマーク結果項目> 結果一覧) {
        for(var インデックス = 結果一覧.Count-1;インデックス>=0;インデックス--) {
            var 結果 = 結果一覧[インデックス];
            if(結果.ソースタブ==行.ソースタブ.タブ
                &&string.Equals(結果.プロジェクト,ベンチマーク.プロジェクト,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(結果.メソッド,ベンチマーク.メソッド名,StringComparison.OrdinalIgnoreCase)
                &&ベンチマーク型一致ロジック.同じ型か(結果.型完全名,ベンチマーク.型完全名)
                &&同じ引数か(結果.引数キー,ベンチマーク.引数キー)
                &&終了結果か(結果)) {
                return 結果;
            }
        }

        return null;
    }

    private UI実行状態 スナップショット状態を解決(ベンチマークプロジェクトセル セル,ベンチマーク結果項目? 結果) {
        if(結果 is not null) {
            if(string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)) {
                return UI実行状態.完了;
            }
            if(string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase)) {
                return UI実行状態.失敗;
            }
        }
        if(セル.測定値.測定値ありか) {
            return UI実行状態.完了;
        }
        if(string.Equals(セル.状態画像ソース,ベンチマーク状態画像名.失敗,StringComparison.OrdinalIgnoreCase)) {
            return UI実行状態.失敗;
        }

        return UI実行状態.待機中;
    }

    private static int? CPU番号を解析(string 値) =>
        int.TryParse(値,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var 解析済み) ? 解析済み : null;

    private static ベンチマーク結果項目 スナップショット結果項目を作成(
        ベンチマークソースタブ定義 ソースタブ,
        string プロジェクト,
        string アセンブリ名,
        string 型完全名,
        string メソッド名,
        string 引数キー,
        IReadOnlyList<ベンチマーク引数> 引数一覧,
        UI実行状態 状態,
        int? 要素数,
        int? CPU番号,
        DateTimeOffset? 測定済み日時,
        double? 平均ナノ秒,
        long? 割り当てバイト数,
        string 失敗理由) {
        return new ベンチマーク結果項目(new ベンチマークジョブ {
            プロジェクト=プロジェクト,
            アセンブリ名=アセンブリ名,
            型完全名=型完全名,
            メソッド名=メソッド名,
            引数キー=引数キー,
            引数一覧=引数一覧.ToList(),
            ソースタブ=ソースタブ.タブ,
            要素数=要素数??0,
            ループ回数=ベンチマーク作業軸.既定ループ回数,
            状態=再開用ジョブ状態へ変換(状態),
            平均=平均ナノ秒?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            平均ナノ秒=平均ナノ秒,
            割り当てバイト数=割り当てバイト数,
            開始日時=測定済み日時,
            終了日時=測定済み日時,
            割り当て論理CPU=CPU番号,
            失敗理由=失敗理由,
        });
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
    /// 表示結果から比較行を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 表示結果から比較行を更新() {
        var 選択値要素数 = this.選択結果要素数?.値;
        if(this.表示に今回実行メモリを使うか(this.選択ソース定義)) {
            this.比較行一覧値=選択値要素数 is { } 現在要素数
                ? this.要素数の現在実行比較行を構築(現在要素数)
                : this.現在実行比較行を構築();
            return;
        }

        if(this.結果項目一覧.Count>0) {
            this.比較行一覧値=選択値要素数 is { } 表示済み要素数
                ? this.要素数の表示行を構築(this.選択ソース定義,表示済み要素数)
                : this.表示行を構築(this.選択ソース定義);
            return;
        }

        this.比較行一覧値= [];
    }

    /// <summary>
    /// 比較行要求トークン フィールドです。
    /// </summary>
    private int 比較行要求トークン;
    /// <summary>
    /// 比較行更新中か フィールドです。
    /// </summary>
    private bool 比較行更新中か;
    /// <summary>
    /// 比較行更新保留中か フィールドです。
    /// </summary>
    private bool 比較行更新保留中か;

    /// <summary>
    /// 比較行タブ入力 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="現在実行結果一覧">現在実行結果一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct 比較行タブ入力(
        ベンチマークソースタブ表示状態 状態,
        IReadOnlyList<ベンチマーク結果項目> 現在実行結果一覧);

    // 全タブ分の比較行再構築は結果項目一覧全体のLINQ走査とDBの前回結果参照を伴う。
    // を伴い重い。現在実行セッション/ResultItemsに触れる部分だけをUIスレッドで同期的にスナップショット
    // し、残りの純粋計算(フィルタ・整形・DB参照)はバックグラウンドスレッドで行い、
    // state.比較行一覧(ObservableCollectionではない単純プロパティ)への代入だけをUIスレッドに戻す。
    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void 全比較行を表示結果から更新() {
        if(this.比較行更新中か) {
            this.比較行更新保留中か=true;
            return;
        }

        var 選択値要素数 = this.選択結果要素数?.値;
        var タブ入力一覧 = this.ソースタブ表示状態一覧値
            .Select(状態 => new 比較行タブ入力(状態,this.現在実行結果項目一覧を取得(状態.定義.タブ)))
            .ToArray();
        var 結果項目一覧スナップショット = this.結果項目一覧.ToArray();
        var トークン = ++this.比較行要求トークン;
        var 世代 = this.実行結果世代;
        this.比較行更新中か=true;
        var ストップウォッチ = Stopwatch.StartNew();
        Task.Run(() => タブ入力一覧
                .Select(入力 => this.タブの比較行を計算(入力,結果項目一覧スナップショット,選択値要素数))
                .ToArray())
            .ContinueWith(タスク => {
                UIスレッド予算.記録("bg:comparison",ストップウォッチ.Elapsed.TotalMilliseconds);
                this.UIディスパッチャー.呼び出し開始(() => {
                    this.比較行更新中か=false;
                    if(トークン==this.比較行要求トークン&&世代==this.実行結果世代&&!タスク.IsFaulted) {
                        var 計算結果 = タスク.Result;
                        UIスレッド予算.計測("comparison-apply",() => {
                            for(var インデックス = 0;インデックス<タブ入力一覧.Length;インデックス++) {
                                タブ入力一覧[インデックス].状態.比較行一覧=計算結果[インデックス];
                                this.ベンチマークシェア概要を更新(タブ入力一覧[インデックス].状態);
                            }
                            // 新しいComparisonRowsを行/スキップセル/完了状態へ反映する。もう次のtickが
                            // 来ない（停止中実行の閲覧時など）場合でもここで確定させないと表示が更新されないまま残る。
                            this.比較行を全表へ適用();
                            if(選択値要素数 is { } スキップ済み要素数) {
                                this.スキップセルを適用(スキップ済み要素数);
                            }
                            this.最終完了状態を更新();
                        });
                    } else if(タスク.IsFaulted) {
                        起動診断.書き込む("バックグラウンドでの表示結果からの全比較行更新に失敗しました。",タスク.Exception);
                    }

                    if(this.比較行更新保留中か) {
                        this.比較行更新保留中か=false;
                        this.全比較行を表示結果から更新();
                    }
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// タブの比較行を計算 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="入力">入力 を指定して実行条件を制御します。</param>
    /// <param name="結果項目一覧スナップショット">結果項目一覧スナップショット を指定して実行条件を制御します。</param>
    /// <param name="選択値要素数">選択値要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> タブの比較行を計算(
        比較行タブ入力 入力,
        IReadOnlyList<ベンチマーク結果項目> 結果項目一覧スナップショット,
        int? 選択値要素数) {
        if(入力.現在実行結果一覧.Count>0&&!保存DB実行選択肢か(this.選択実行選択肢)) {
            return 選択値要素数 is { } 現在要素数
                ? this.実行投影サービス.要素数の比較行を構築(入力.現在実行結果一覧,入力.状態.定義,現在要素数,this.比較用報告行を作成)
                : this.実行投影サービス.最新比較行を構築(入力.現在実行結果一覧,入力.状態.定義,this.比較用報告行を作成);
        }

        if(結果項目一覧スナップショット.Count>0) {
            return this.実行投影サービス.選択要素数の表示行を構築(結果項目一覧スナップショット,入力.状態.定義,選択値要素数,this.比較用報告行を作成);
        }

        return [];
    }

    private void 全比較行を読込結果項目から更新(IReadOnlyList<ベンチマーク結果項目> 結果項目一覧スナップショット) {
        var 選択値要素数 = this.選択結果要素数?.値;
        foreach(var 状態 in this.ソースタブ表示状態一覧値) {
            状態.比較行一覧=this.実行投影サービス.選択要素数の表示行を構築(
                結果項目一覧スナップショット,
                状態.定義,
                選択値要素数,
                this.比較用報告行を作成);
            this.ベンチマークシェア概要を更新(状態);
        }

        this.比較行一覧値=this.選択ソースタブ表示状態.比較行一覧;
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

    private void 保存DB実行から新規実行用カタログ行を復元() {
        var 表示済み選択一覧 = this.表示ベンチマーク行選択を取得();
        this.新規実行用の新規実行選択肢を選択(false);
        this.新規実行用に全現在実行結果をクリア();
        this.カタログからベンチマーク行を再構築();
        this.表示ベンチマーク行選択を適用(表示済み選択一覧);
    }

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
    /// UI実行状態をキューへ追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="実行ID">実行ID を指定して実行条件を制御します。</param>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="メッセージ">メッセージ を指定して実行条件を制御します。</param>
    private void UI実行状態をキューへ追加(long? 実行ID,UI実行状態 状態,string メッセージ) {
        this.UI実行リポジトリ.実行状態設定を予約(実行ID,状態,メッセージ);
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

    private IReadOnlyList<ベンチマーク結果項目> 永続化用グリッドスナップショット結果を作成(IReadOnlyList<ベンチマーク永続化項目> 項目一覧) {
        var グリッド結果一覧 = this.全グリッド結果項目を取得();
        var 保留結果一覧 = 項目一覧
            .Where(項目 => 項目.実行世代==this.実行結果世代&&終了結果か(項目.結果))
            .Select(static 項目 => 項目.結果)
            .ToArray();
        if(保留結果一覧.Length==0) {
            return グリッド結果一覧;
        }
        if(グリッド結果一覧.Count==0) {
            return 保留結果一覧;
        }

        return グリッド結果一覧.Concat(保留結果一覧).ToArray();
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
    /// 実行UIスナップショットを適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="スナップショット">スナップショット を指定して実行条件を制御します。</param>
    private void 実行UIスナップショットを適用(ベンチマークUIスナップショット スナップショット) {
        if(!スナップショット.更新ありか) {
            return;
        }

        UIスレッド予算.計測("running-apply",() => {
            foreach(var 項目 in スナップショット.実行中更新一覧) {
                this.実行中スナップショット結果を適用(項目);
            }
        });
        UIスレッド予算.計測("terminal-apply",() => {
            foreach(var 項目 in スナップショット.終端結果一覧) {
                this.終了スナップショット結果を適用(項目);
            }
        });
        if(スナップショット.進捗を更新するか) {
            UIスレッド予算.計測("progress-dispatch",this.実行中結果用ソースタブ進行を更新);
        }
        if(スナップショット.終端結果一覧.Count>0) {
            UIスレッド予算.計測("share-summary-dispatch",this.全比較行を表示結果から更新);
            if(this.ベンチマーク実行有効か) {
                UIスレッド予算.計測("completion-dispatch",this.最終完了状態を更新);
            } else {
                UIスレッド予算.計測("idle-refresh",() => {
                    this.結果確認を更新(this.選択ソース定義.タブ);
                    this.表示結果を更新();
                    this.選択要素数を表へ適用();
                    this.最終完了状態を更新();
                    this.ソースタブ進行を更新();
                });
            }
        }
        this.保留最終ライブ表示クリアの適用を試みる();
        this.実行後表示確定の適用を試みる();
    }

    /// <summary>
    /// 実行中スナップショット結果を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="項目">項目 を指定して実行条件を制御します。</param>
    private void 実行中スナップショット結果を適用(ベンチマーク実行イベント 項目) {
        if(項目.実行世代!=this.実行結果世代) {
            return;
        }
        if(!this.ベンチマーク実行有効か) {
            return;
        }

        var 結果 = 項目.結果;
        起動診断.パンくずを追加(this.結果パンくずを作成("実行中スナップショット結果",結果));
        var CPU = this.割当CPUを検索(結果);
        var 行 = this.ベンチマーク行を検索(結果);
        CPU?.ジョブを適用(結果,行?.ベンチマークメソッド文字列);
        this.ソースタブ実行中結果を適用(結果);
        this.有効実行ソースタブを記録(結果.ソースタブ);
        if(結果.軸数>0) {
            this.現在実行結果項目を追加または更新(結果);
            if(結果.ソースタブ==this.選択ソース定義.タブ) {
                this.現在要素数文字列=要素数文字列を整形(結果.軸数);
            }
        }
        if(結果.ソースタブ!=this.選択ソース定義.タブ) {
            return;
        }

        this.表示実行中結果をグリッドセルへ適用(
            結果,
            CPU,
            DateTimeOffset.Now,
            自動スクロールを要求するか: false,
            完了強調を維持するか: false);
    }

    /// <summary>
    /// 終了スナップショット結果を適用 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="項目">項目 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private void 終了スナップショット結果を適用(ベンチマーク実行イベント 項目) {
        if(項目.実行世代!=this.実行結果世代) {
            return;
        }

        var 結果 = 項目.結果;
        起動診断.パンくずを追加(this.結果パンくずを作成("終端スナップショット結果",結果));
        if(string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase)) {
            起動診断.書き込む(ベンチマーク失敗表示ロジック.ログ文を作成(
                結果.プロジェクト,
                結果.メソッド,
                結果.軸数,
                結果.割り当て論理CPU,
                結果.成果物フォルダー,
                結果.失敗理由,
                結果.型完全名));
        }

        // FlushNow は BeginInvoke 予約であり、終端イベントは MaxTerminalResultsPerTick 件ずつの
        // 分割描画のため、実行終了(finally)後に残りが描画されることがある。その場合はデータ反映のみ
        // 行い、ライブ表示(CPUセル/タブ実行状態/行)には書かない。最終クリーンアップを最後の書き手にするため。
        var ライブ表示有効か = this.ベンチマーク実行有効か;
        var CPU = this.割当CPUを検索(結果);
        var 行 = ライブ表示有効か ? this.ベンチマーク行を検索(結果) : null;
        if(ライブ表示有効か) {
            CPU?.ジョブを適用(結果,行?.ベンチマークメソッド文字列);
            this.ソースタブ実行中結果を適用(結果);
        }
        if(結果.軸数>0) {
            this.現在実行結果項目を追加または更新(結果);
            if(結果.ソースタブ==this.選択ソース定義.タブ) {
                this.現在要素数文字列=要素数文字列を整形(結果.軸数);
                this.結果項目を追加または更新(結果);
                this.結果要素数を登録(結果.軸数);
            }
            if(ライブ表示有効か) {
                行?.実行結果を適用(結果,CPU,DateTimeOffset.Now);
            }
        }
        this.終了状態保存保留ありか=true;
        this.未保存ベンチマーク結果ありか=true;
        this.ベンチマーク結果保存をキューへ追加(結果,項目.実行世代,項目.UI実行ID);
    }

    /// <summary>
    /// CPUジョブをクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void CPUジョブをクリア() {
        foreach(var CPU in this.CPU項目一覧) {
            CPU.ジョブをクリア();
        }
    }

    /// <summary>
    /// 実行を切り替え メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 実行を切り替え() {
        _=this.実行または再開Async();
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
    /// 実行中結果か メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 実行中結果か(ベンチマーク結果項目 結果) =>
        ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.実行中);

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

    /// <summary>
    /// 割当CPUを検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private CPU選択項目? 割当CPUを検索(ベンチマーク結果項目 結果) {
        var 論理CPU = ベンチマーク結果CPU表示ロジック.割り当て論理CPUを解決(
            結果.割り当て論理CPU,
            this.CPU項目一覧.Select(static 項目 => 項目.論理プロセッサインデックス));
        return 論理CPU is { } 値
            ? this.CPU項目一覧.FirstOrDefault(項目 => 項目.論理プロセッサインデックス==値)
            : null;
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

    /// <summary>
    /// 結果行キーを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="結果">結果 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string 結果行キーを作成(ベンチマーク結果項目 結果) =>
        this.ベンチマーク行を検索(結果)?.行キー??結果.メソッド;

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
    /// 選択要素数一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<int> 選択要素数一覧を取得() {
        return ベンチマーク要素数ロジック
            .適応候補を作成(ベンチマーク要素数一覧.全て,ベンチマーク要素数一覧.全て[^1])
            .ToArray();
    }

    /// <summary>
    /// ベンチマークルートを取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="プロファイル">プロファイル を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string ベンチマークルートを取得(ベンチマーク実行プロファイル プロファイル) =>
        Path.Combine(this.ベンチマークルート,ベンチマーク実行プロファイル名.フォルダー名に変換(プロファイル));

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
    /// 表示結果を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 表示結果を更新() {
        this.表示結果項目一覧.Clear();
        var 選択値要素数 = this.選択結果要素数?.値;
        foreach(var 項目 in this.実行投影サービス.表示結果を取得(this.結果項目一覧.Where(this.結果が表示選択中か),選択値要素数)) {
            this.表示結果項目一覧.Add(項目);
        }
    }

    /// <summary>
    /// 選択タブに現在実行結果があるか プロパティです。
    /// </summary>
    /// <value>選択タブに現在実行結果があるか の値を取得/設定します。</value>
    private bool 選択タブに現在実行結果があるか =>
        this.現在実行セッション.ソースタブ結果があるか(this.選択ソース定義.タブ);

    private bool 表示に今回実行メモリを使うか(ベンチマークソースタブ定義 ソースタブ) =>
        !保存DB実行選択肢か(this.選択実行選択肢)
        &&ベンチマーク実行表示元ロジック.今回実行メモリを使うか(this.現在実行セッション.ソースタブ結果があるか(ソースタブ.タブ));

    /// <summary>
    /// 表示結果があるか プロパティです。
    /// </summary>
    /// <value>表示結果があるか の値を取得/設定します。</value>
    private bool 表示結果があるか =>
        this.結果項目一覧.Count>0
        ||this.選択タブに現在実行結果があるか
        ||this.現在実行セッション.結果ありか;

    /// <summary>
    /// 保存可能ベンチマーク結果があるか プロパティです。
    /// </summary>
    /// <value>保存可能ベンチマーク結果があるか の値を取得/設定します。</value>
    private bool 保存可能ベンチマーク結果があるか =>
        this.全グリッド結果項目を取得().Any(終了結果か);

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
    /// 選択タブの現在実行結果項目一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク結果項目> 選択タブの現在実行結果項目一覧を取得() =>
        this.現在実行結果項目一覧を取得(this.選択ソース定義.タブ);

    /// <summary>
    /// 現在実行結果項目一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク結果項目> 現在実行結果項目一覧を取得(ベンチマークソースタブ ソースタブ) =>
        this.現在実行セッション.結果を取得(ソースタブ);

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

    private void 表示結果を現在実行セッションへ昇格() {
        foreach(var 結果 in this.結果項目一覧) {
            this.現在実行結果項目を追加または更新(結果);
        }
    }

    private static bool 終了結果か(ベンチマーク結果項目 結果) =>
        string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);

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
    /// 選択実行表示をクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択実行表示をクリア() {
        using(this.データグリッド更新バッチを開始()) {
            this.ソースタブ実行中状態をクリア();
            this.全ソースタブ比較表示をクリア();
            this.フォールバック用完了結果項目を記憶();
            this.結果項目一覧.Clear();
            this.表示結果項目一覧.Clear();
            this.現在要素数文字列="-";
            this.ベンチマーク結果表示をクリア();
            this.改善プロンプト文字列=string.Empty;
            this.ソースタブ進行を更新();
            this.ツールバーコマンド状態を更新();
        }
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
        this.選択実行状態キャッシュ=null;
        this.実行結果強調をクリア();
        this.ソースタブ進行を更新();
        this.ツールバーコマンド状態を更新();
    }

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
    /// 比較行を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 比較行を表へ適用() {
        this.比較行を表へ適用(this.選択ソースタブ表示状態);
    }

    /// <summary>
    /// 比較行を全表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 比較行を全表へ適用() {
        foreach(var 状態 in this.ソースタブ表示状態一覧値) {
            this.比較行を表へ適用(状態);
        }
    }

    /// <summary>
    /// 比較行を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    private void 比較行を表へ適用(ベンチマークソースタブ表示状態 状態) {
        var 現在実行結果ありか = this.現在実行セッション.ソースタブ結果があるか(状態.定義.タブ);
        foreach(var ベンチマーク in 状態.行一覧) {
            var 比較行 = this.ベンチマーク実行有効か||現在実行結果ありか
                ? this.今回実行比較行を検索(状態,ベンチマーク)
                : this.比較行を検索(状態,ベンチマーク);
            ベンチマーク.比較を適用(比較行,this.ベンチマーク実行有効か||現在実行結果ありか);
        }
        this.ベンチマークシェア概要を更新(状態);
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
    /// 選択表示結果を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択表示結果を表へ適用() {
        var 選択値要素数 = this.選択結果要素数?.値;
        var ソース = this.表適用用結果ソースを取得();
        var 結果一覧 = 選択値要素数 is null
            ? ソース
            : ソース.Where(結果 => 結果.軸数==選択値要素数).ToArray();
        if(結果一覧.Count==0&&ソース.Count>0) {
            結果一覧=ソース;
        }
        foreach(var 結果 in 結果一覧) {
            var 行 = this.ベンチマーク行を検索(結果);
            行?.実行結果を適用(結果,this.割当CPUを検索(結果),結果.開始日時??DateTimeOffset.Now,false);
        }
    }

    /// <summary>
    /// 比較行を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク比較行? 比較行を検索(ベンチマーク表行 行) {
        return this.比較行を検索(this.選択ソースタブ表示状態,行);
    }

    /// <summary>
    /// 比較行を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク比較行? 今回実行比較行を検索(ベンチマークソースタブ表示状態 状態,ベンチマーク表行 行) {
        if(!行.選択中か) {
            return null;
        }

        foreach(var 項目 in 状態.比較行一覧) {
            if(!string.IsNullOrWhiteSpace(項目.型完全名)&&比較行が一致するか(行,項目)) {
                return 項目;
            }
        }

        return null;
    }

    private ベンチマーク比較行? 比較行を検索(ベンチマークソースタブ表示状態 状態,ベンチマーク表行 行) {
        foreach(var 項目 in 状態.比較行一覧) {
            if(比較行が一致するか(行,項目)) {
                return 項目;
            }
        }

        return null;
    }

    /// <summary>
    /// ベンチマークシェア概要を更新します。
    /// </summary>
    private void ベンチマークシェア概要を更新() {
        this.ベンチマークシェア概要を更新(this.選択ソースタブ表示状態);
    }

    /// <summary>
    /// ベンチマークシェア概要を更新します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    private void ベンチマークシェア概要を更新(ベンチマークソースタブ表示状態 状態) {
        var 測定済み行一覧 = this.測定実行比較行を構築(状態.定義);
        var 概要 = ベンチマーク改善プロンプトビルダー.概要を作成(測定済み行一覧);
        状態.ベンチマークシェア概要文=概要.表示文字列;
        this.ソースタブ選択肢を取得(状態.定義.タブ).ベンチマークシェア概要短文を設定(概要.短縮表示文字列);
        this.全体ベンチマークシェア概要を更新();
        if(状態==this.選択ソースタブ表示状態) {
            this.OnPropertyChanged(nameof(this.ベンチマークシェア概要文));
        }
    }

    private IReadOnlyList<ベンチマーク比較行> 測定実行比較行を構築(ベンチマークソースタブ定義 ソースタブ) {
        var 測定済み = this.表示用結果ソースを取得()
            .Where(結果 => 結果.実行内測定か&&ソースタブ.プロジェクトを含むか(結果.プロジェクト))
            .ToArray();
        return 測定済み.Length==0
            ? []
            : this.実行投影サービス.最新比較行を構築(測定済み,ソースタブ,this.比較用報告行を作成);
    }

    private void 全体ベンチマークシェア概要を更新() {
        var 行一覧 = this.ソースタブ表示状態一覧値
            .SelectMany(状態 => this.測定実行比較行を構築(状態.定義))
            .ToArray();
        var 概要 = ベンチマーク改善プロンプトビルダー.概要を作成(行一覧);
        this.全体ベンチマークシェア概要文=概要.全体表示文字列;
        if(this.全体ベンチマークシェア概要短文!=概要.短縮表示文字列) {
            this.全体ベンチマークシェア概要短文=概要.短縮表示文字列;
            this.OnPropertyChanged(nameof(this.全体ソース進行概要文字列));
        }
    }

    /// <summary>
    /// 改善プロンプトを更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 改善プロンプトを更新() {
        var 候補一覧 = this.比較行から改善候補を構築();
        if(候補一覧.Count==0) {
            候補一覧=ベンチマーク改善候補ビルダー.候補を構築(this.改善プロンプト用進行状態一覧を取得());
        }
        this.改善候補を適用(候補一覧);
        this.改善プロンプト文字列を更新();
    }

    private IReadOnlyList<ベンチマーク改善候補> 比較行から改善候補を構築() {
        var 行一覧 = this.ソースタブ表示状態一覧値
            .SelectMany(static 状態 => 状態.比較行一覧)
            .ToArray();
        if(行一覧.Length==0) {
            return [];
        }

        var 要素数 = this.選択結果要素数?.値??0;
        return ベンチマーク改善候補ビルダー.比較行から候補を構築(行一覧,要素数);
    }

    /// <summary>
    /// 改善候補を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="候補一覧">候補一覧 を指定して実行条件を制御します。</param>
    private void 改善候補を適用(IReadOnlyList<ベンチマーク改善候補> 候補一覧) {
        this.現在実行の改善候補選択を読み込む();
        var 保存選択ありか = this.改善候補選択読込済みか&&this.改善候補選択状態.Count>0;
        var 既定選択値 = 保存選択ありか
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ベンチマーク改善候補ビルダー.既定選択キーを作成(候補一覧);
        foreach(var 行 in this.改善候補一覧) {
            行.選択変更時-=this.改善候補選択変更時;
        }
        this.改善候補一覧.Clear();
        this.改善候補選択適用中か=true;
        try {
            foreach(var 候補 in 候補一覧) {
                var 選択中か = 保存選択ありか
                    ? this.改善候補選択状態.TryGetValue(候補.キー,out var 値か)&&値か
                    : 既定選択値.Contains(候補.キー);
                this.改善候補選択状態[候補.キー]=選択中か;
                var 行 = new ベンチマーク改善候補行(候補,選択中か);
                行.選択変更時+=this.改善候補選択変更時;
                this.改善候補一覧.Add(行);
            }
        } finally {
            this.改善候補選択適用中か=false;
        }
        this.改善候補概要文字列=string.Empty;
    }

    /// <summary>
    /// 改善候補選択変更時 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="送信元">送信元 を指定して実行条件を制御します。</param>
    /// <param name="引数">引数 を指定して実行条件を制御します。</param>
    private void 改善候補選択変更時(object? 送信元,EventArgs 引数) {
        if(送信元 is not ベンチマーク改善候補行 行) {
            return;
        }

        this.改善候補選択状態[行.キー]=行.選択中か;
        if(!this.改善候補選択適用中か) {
            this.改善プロンプト文字列を更新();
            this.改善候補概要文字列=string.Empty;
        }
    }

    /// <summary>
    /// 改善プロンプト文字列を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 改善プロンプト文字列を更新() {
        this.改善プロンプト文字列=ベンチマーク改善プロンプトビルダー.プロンプトを構築(
            this.改善候補一覧
                .Where(static 行 => 行.選択中か)
                .Select(static 行 => 行.候補));
    }

    /// <summary>
    /// 現在実行の改善候補選択を読み込む メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 現在実行の改善候補選択を読み込む() {
        var 実行ID = this.現在改善選択実行IDを取得();
        if(this.読込済み改善選択実行ID==実行ID) {
            return;
        }

        this.読込済み改善選択実行ID=実行ID;
        this.改善候補選択状態.Clear();
        this.改善候補選択読込済みか=false;
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
    /// 改善プロンプトをコピーAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 改善プロンプトをコピーAsync() {
        if(string.IsNullOrWhiteSpace(this.改善プロンプト文字列)) {
            return;
        }

        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(this.改善プロンプト文字列);
        this.状態文字列="改善プロンプトをコピーしました";
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
    /// データグリッド更新バッチ深度 フィールドです。
    /// </summary>
    private int データグリッド更新バッチ深度;
    /// <summary>
    /// 最終実行中進行更新日時 フィールドです。
    /// </summary>
    private DateTimeOffset 最終実行中進行更新日時 = DateTimeOffset.MinValue;
    /// <summary>
    /// 実行中進行更新間隔 フィールドです。
    /// </summary>
    private static readonly TimeSpan 実行中進行更新間隔 = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// データグリッド更新バッチ変更時 イベントです。
    /// </summary>
    internal event EventHandler<ベンチマークデータグリッド更新バッチイベント引数>? データグリッド更新バッチ変更時;

    /// <summary>
    /// 保存済み実行の読込中に一部の行がスキップされたときに発生するイベントです。
    /// イベント引数はユーザーへ表示するための詳細メッセージです。
    /// </summary>
    internal event EventHandler<string>? 読込警告が発生した;

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

    /// <summary>
    /// 改善プロンプト用進行状態一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<進行状態> 改善プロンプト用進行状態一覧を取得() {
        if(this.現在実行セッション.結果ありか) {
            return this.全タブ結果から進行状態一覧を作成(this.現在未保存実行ルート??string.Empty);
        }

        var 状態 = this.選択実行状態を読み込む();
        return 状態 is null ? [] : [状態];
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

    // 保存値は全タブ/全行のグリッド表示として復元する。再実行可能なケースだけは、
    // タブ・表示型・メソッド・専用引数列を使って現在のカタログへ一意に接続する。
    // 測定値、CPU、日時はDB行を正とし、カタログから推測補完しない。
    private void 読込ベンチマークスナップショット項目を適用(IReadOnlyList<ベンチマークグリッドスナップショット項目> 項目一覧) {
        var 表示項目一覧 = 項目一覧.ToArray();
        this.実行結果世代=unchecked(this.実行結果世代+1);
        this.比較行要求トークン=unchecked(this.比較行要求トークン+1);
        this.比較行更新保留中か=false;
        using(this.進行更新バッチを開始())
        using(this.データグリッド更新バッチを開始()) {
            this.結果項目一覧.Clear();
            this.表示結果項目一覧.Clear();
            this.現在実行セッション.クリア();
            this.ソースタブ実行中状態をクリア();
            this.全ソースタブ比較表示をクリア();
            this.ベンチマーク選択適用中か=true;
            try {
                foreach(var 状態 in this.ソースタブ表示状態一覧値) {
                    var 状態項目一覧 = 表示項目一覧
                        .Where(項目 => string.Equals(項目.ソースタブID,状態.定義.ID,StringComparison.OrdinalIgnoreCase))
                        .OrderBy(static 項目 => 項目.順序)
                        .ToArray();
                    var 行一覧 = this.スナップショット行を作成(状態.定義,状態項目一覧).ToArray();
                    状態.行一覧.範囲を置換(行一覧);
                    for(var インデックス = 0;インデックス<行一覧.Length;インデックス++) {
                        var 行 = 行一覧[インデックス];
                        var 項目 = 状態項目一覧[インデックス];
                        this.ベンチマーク行を購読(行);
                        this.結果項目一覧.Add(this.読込スナップショット結果項目を作成(状態.定義,項目,状態.定義.左プロジェクト,項目.左));
                        if(項目.中央 is { } 中央&& !string.IsNullOrWhiteSpace(状態.定義.中央プロジェクト)) {
                            this.結果項目一覧.Add(this.読込スナップショット結果項目を作成(状態.定義,項目,状態.定義.中央プロジェクト,中央));
                            this.読込ベンチマークスナップショット側を適用(行,状態.定義.中央プロジェクト,項目.CPU番号,中央);
                        }
                        if(項目.追加 is { } 追加&& !string.IsNullOrWhiteSpace(状態.定義.追加プロジェクト)) {
                            this.結果項目一覧.Add(this.読込スナップショット結果項目を作成(状態.定義,項目,状態.定義.追加プロジェクト,追加));
                            this.読込ベンチマークスナップショット側を適用(行,状態.定義.追加プロジェクト,項目.CPU番号,追加);
                        }
                        this.結果項目一覧.Add(this.読込スナップショット結果項目を作成(状態.定義,項目,状態.定義.右プロジェクト,項目.右));
                        this.読込ベンチマークスナップショット側を適用(行,状態.定義.左プロジェクト,項目.CPU番号,項目.左);
                        this.読込ベンチマークスナップショット側を適用(行,状態.定義.右プロジェクト,項目.CPU番号,項目.右);
                    }
                }

                this.ベンチマーク行インデックスを再構築();
                foreach(var 項目 in 表示項目一覧) {
                    if(ベンチマークソースタブ一覧.IDから取得を試みる(項目.ソースタブID) is null) {
                        this.スナップショット診断を記録(項目,"ソースタブID を解決できません。");
                    }
                }

            } finally {
                this.ベンチマーク選択適用中か=false;
            }

            var 読込済み要素件数一覧 = this.結果項目一覧
                .Select(static 項目 => 項目.軸数)
                .Where(static 項目 => 項目>0)
                .Distinct()
                .Order()
                .ToArray();
            // 結果要素数一覧を再構築 は 選択結果要素数 を更新する。
            // 保存済み実行の読込中に通常の選択軸変更処理が動くと、非同期の比較行再反映が
            // ClearComparison(false) を通り、直前に復元したスナップショットセルを消す。
            this.結果要素数適用中か=true;
            try {
                this.結果要素数一覧を再構築(読込済み要素件数一覧);
                if(this.選択結果要素数 is null
                    ||読込済み要素件数一覧.Length>0&&!読込済み要素件数一覧.Contains(this.選択結果要素数.値)) {
                    this.選択結果要素数=this.結果要素数選択肢一覧.LastOrDefault();
                }
            } finally {
                this.結果要素数適用中か=false;
            }
            this.表示結果を更新();
            this.全比較行を読込結果項目から更新(this.結果項目一覧.ToArray());
            this.ソースタブ進行を更新();
            this.改善プロンプトを更新();
            this.現在ソートを適用();
        }
        // 進捗/ソートなどの通常UI更新後、スナップショットのセル値を最後に再適用する。
        // 保存済み実行の表示ではこのDB行単位の復元が正であり、比較表示用の行再計算は正規読込経路ではない。
        this.ベンチマーク選択適用中か=true;
        try {
            this.ベンチマークスナップショット項目を行へ適用();
            this.ベンチマークスナップショット最終完了状態を適用();
            this.読込スナップショットグリッド表示欠落を記録();
        } finally {
            this.ベンチマーク選択適用中か=false;
        }
        if(this.ベンチマーク行一覧.Count==0) {
            var メッセージ = "保存済みベンチマーク実行スナップショットの読み込みで、選択タブの行が生成されませんでした "
                +this.選択ソース定義.ID
                +" 項目数="
                +項目一覧.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 表示項目数="
                +表示項目一覧.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 実行="
                +(this.選択実行選択肢?.UI実行ID.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-");
            起動診断.書き込む(メッセージ);
            this.状態文字列="DB読み込み失敗: 表示行が空です";
        }
    }

    private void 読込スナップショットグリッド表示欠落を記録() {
        foreach(var (項目,行,ソースタブ) in this.スナップショットバインディングを列挙()) {
            this.読込スナップショットグリッド表示欠落を記録(項目,行,ソースタブ.左プロジェクト,項目.左,行.左セル);
            if(項目.中央 is { } 中央&& !string.IsNullOrWhiteSpace(ソースタブ.中央プロジェクト)) {
                this.読込スナップショットグリッド表示欠落を記録(項目,行,ソースタブ.中央プロジェクト,中央,行.中央セル);
            }
            if(項目.追加 is { } 追加&& !string.IsNullOrWhiteSpace(ソースタブ.追加プロジェクト)) {
                this.読込スナップショットグリッド表示欠落を記録(項目,行,ソースタブ.追加プロジェクト,追加,行.追加セル);
            }
            this.読込スナップショットグリッド表示欠落を記録(項目,行,ソースタブ.右プロジェクト,項目.右,行.右セル);
        }
    }

    private void 読込スナップショットグリッド表示欠落を記録(
        ベンチマークグリッドスナップショット項目 項目,
        ベンチマーク表行 行,
        string プロジェクト,
        ベンチマークグリッドスナップショット側 側,
        ベンチマークプロジェクトセル セル) {
        if(側.平均ナノ秒 is null||セル.測定値.測定値ありか) {
            return;
        }

        起動診断.書き込む(
            "保存済みベンチマーク実行スナップショットの指標をグリッドセルへ反映できませんでした。"
            +" 実行ID="+項目.実行ID.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" ソースタブID="+項目.ソースタブID
            +" プロジェクト="+プロジェクト
            +" 型="+項目.型名
            +" ベンチマーク="+項目.ベンチマーク名
            +" 行キー="+行.行キー
            +" スナップショット平均ns="+側.平均ナノ秒.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private ベンチマーク結果項目 読込スナップショット結果項目を作成(
        ベンチマークソースタブ定義 ソースタブ,
        ベンチマークグリッドスナップショット項目 項目,
        string プロジェクト,
        ベンチマークグリッドスナップショット側 側) {
        return スナップショット結果項目を作成(
            ソースタブ,
            プロジェクト,
            ソースタブ.アセンブリ名を取得(プロジェクト),
            項目.型名,
            側.ベンチマーク?.メソッド名??項目.ベンチマーク名,
            側.ベンチマーク?.引数キー??string.Empty,
            側.ベンチマーク?.引数一覧?? [],
            側.状態,
            側.結果項目.要素数>0 ? 側.結果項目.要素数 : (int?)null,
            スナップショット側CPU番号を解決(項目.CPU番号,側),
            側.計測日時,
            側.平均ナノ秒,
            側.割り当てバイト数,
            側.失敗理由);
    }

    private void ベンチマークスナップショット項目を行へ適用() {
        foreach(var (項目,行,ソースタブ) in this.スナップショットバインディングを列挙()) {
            this.読込ベンチマークスナップショット側を適用(行,ソースタブ.左プロジェクト,項目.CPU番号,項目.左);
            if(項目.中央 is { } 中央&& !string.IsNullOrWhiteSpace(ソースタブ.中央プロジェクト)) {
                this.読込ベンチマークスナップショット側を適用(行,ソースタブ.中央プロジェクト,項目.CPU番号,中央);
            }
            if(項目.追加 is { } 追加&& !string.IsNullOrWhiteSpace(ソースタブ.追加プロジェクト)) {
                this.読込ベンチマークスナップショット側を適用(行,ソースタブ.追加プロジェクト,項目.CPU番号,追加);
            }
            this.読込ベンチマークスナップショット側を適用(行,ソースタブ.右プロジェクト,項目.CPU番号,項目.右);
        }
    }

    private static void 行UI選択を設定(ベンチマーク表行 行,bool 選択中か) {
        foreach(var セル in 行.プロジェクトセル一覧) {
            セル.UI選択中か=セル.ケースありか&&選択中か;
        }
        行.UI選択状態を更新();
    }

    private void ベンチマークスナップショット最終完了状態を適用() {
        foreach(var (項目,行,ソースタブ) in this.スナップショットバインディングを列挙()) {
            var 測定一覧 = new List<ベンチマーク要素測定> {
                スナップショット測定を作成(項目.左.結果項目),
            };
            var 進行セル一覧 = new List<ベンチマークタブ進行セル> {
                進行セルを作成(行,ソースタブ.左プロジェクト,true),
            };
            if(項目.中央 is { } 中央&& !string.IsNullOrWhiteSpace(ソースタブ.中央プロジェクト)) {
                測定一覧.Add(スナップショット測定を作成(中央.結果項目));
                進行セル一覧.Add(進行セルを作成(行,ソースタブ.中央プロジェクト,true));
            }
                if(項目.追加 is { } 追加&& !string.IsNullOrWhiteSpace(ソースタブ.追加プロジェクト)) {
                    測定一覧.Add(スナップショット測定を作成(追加.結果項目));
                    進行セル一覧.Add(進行セルを作成(行,ソースタブ.追加プロジェクト,true));
                }
            測定一覧.Add(スナップショット測定を作成(項目.右.結果項目));
            進行セル一覧.Add(進行セルを作成(行,ソースタブ.右プロジェクト,true));
            var 閾値ナノ秒 = this.適応閾値を取得().TotalMilliseconds*1_000_000;
            var 最終完了か = ベンチマーク作業軸.ループ回数を使うか(ソースタブ.タブ)
                ? ベンチマーク行完了ロジック.ループ回数タブ最終完了か(測定一覧,進行セル一覧,閾値ナノ秒)
                : ベンチマーク行完了ロジック.タブ最終完了か(
                    測定一覧,
                    進行セル一覧,
                    測定一覧.Max(static 測定 => 測定.要素数),
                    閾値ナノ秒);
            行.最終完了状態を設定(項目.選択中か&&最終完了か);
        }
    }

    private IEnumerable<(ベンチマークグリッドスナップショット項目 項目,ベンチマーク表行 行,ベンチマークソースタブ定義 ソースタブ)> スナップショットバインディングを列挙() {
        foreach(var ソースタブ in ベンチマークソースタブ一覧.全て) {
            var 状態 = this.ソースタブ表示状態を取得(ソースタブ.タブ);
            foreach(var 行 in 状態.行一覧) {
                if(行.読込済みスナップショット is { } 項目) {
                    yield return (項目,行,ソースタブ);
                }
            }
        }
    }

    private static ベンチマーク要素測定 スナップショット測定を作成(ベンチマーク結果項目 結果) =>
        new(
            結果.プロジェクト,
            結果.メソッド,
            結果.型完全名,
            結果.引数キー,
            結果.軸数,
            結果.状態,
            結果.平均ナノ秒,
            結果.合計ナノ秒);

    // 保存済み実行の読込は「単純な読み込み」とする: 現在のライブカタログへ再照合せず、
    // DBに保存されていたケース情報(プロジェクト・型完全名・メソッド・引数キー等)をそのまま使う。
    // ライブカタログとの照合(型不明時の曖昧な一致を含みうる処理)は「新規」グリッド構築側だけの責務とする。
    private IEnumerable<ベンチマーク表行> スナップショット行を作成(ベンチマークソースタブ定義 ソースタブ,IReadOnlyList<ベンチマークグリッドスナップショット項目> 項目一覧) {
        return 項目一覧
            .Where(項目 => string.Equals(項目.ソースタブID,ソースタブ.ID,StringComparison.OrdinalIgnoreCase))
            .OrderBy(static 項目 => 項目.順序)
            .Select(項目 => {
                var ケース一覧 = new Dictionary<string,ベンチマークケース>(StringComparer.OrdinalIgnoreCase);
                if(保存ケースを取得(項目.左.ベンチマーク) is { } 左ベンチマーク) {
                    ケース一覧[ソースタブ.左プロジェクト]=左ベンチマーク;
                }
                if(項目.中央 is { } 中央
                    &&!string.IsNullOrWhiteSpace(ソースタブ.中央プロジェクト)
                    &&保存ケースを取得(中央.ベンチマーク) is { } 中央ベンチマーク) {
                    ケース一覧[ソースタブ.中央プロジェクト]=中央ベンチマーク;
                }
                if(項目.追加 is { } 追加
                    &&!string.IsNullOrWhiteSpace(ソースタブ.追加プロジェクト)
                    &&保存ケースを取得(追加.ベンチマーク) is { } 追加ベンチマーク) {
                    ケース一覧[ソースタブ.追加プロジェクト]=追加ベンチマーク;
                }
                if(保存ケースを取得(項目.右.ベンチマーク) is { } 右ベンチマーク) {
                    ケース一覧[ソースタブ.右プロジェクト]=右ベンチマーク;
                }
                // 表示名の型プレフィックス("Tsavorite<int>."等)も型列と同じ保存済み表示文字列(項目.型名)を使う。
                // ライブカタログのベンチマークパスへ再照合すると、対応する型が見つからない場合に空へ落ちてしまう。
                var ベンチマーク名 = 項目.ベンチマーク名;
                var 行 = new ベンチマーク表行(
                    スナップショット行キーを作成(項目.型名,項目.ベンチマーク名,スナップショット引数キーを取得(項目)),
                    項目.ベンチマーク名,
                    ベンチマーク名,
                    ソースタブ,
                    ケース一覧,
                    項目.型名) {
                    読込済みスナップショット=項目,
                };
                行.選択中か=項目.選択中か;
                return 行;
            });
    }

    private static ベンチマークケース? 保存ケースを取得(ベンチマークケース? 保存ケース) =>
        保存ケース is not null&&!string.IsNullOrWhiteSpace(保存ケース.型完全名) ? 保存ケース : null;

    private void 読込ベンチマークスナップショット側を適用(
        ベンチマーク表行 行,
        string プロジェクト,
        int? CPU番号,
        ベンチマークグリッドスナップショット側 側) {
        var セル = 行.プロジェクトセルを取得(プロジェクト);
        if(セル is null) {
            return;
        }

        var 側CPU番号 = スナップショット側CPU番号を解決(CPU番号,側);
        行.スナップショット結果を適用(プロジェクト,側.結果項目,this.スナップショットCPUを検索(側CPU番号),側CPU番号,側.計測日時);
    }

    private static int? スナップショット側CPU番号を解決(int? 行CPU番号,ベンチマークグリッドスナップショット側 側) =>
        側.結果項目.割り当て論理CPU??行CPU番号;

    private CPU選択項目? スナップショットCPUを検索(int? CPU番号) =>
        CPU番号 is { } 値
            ? this.CPU項目一覧.FirstOrDefault(項目 => 項目.論理プロセッサインデックス==値)
            : null;

    private static string スナップショット引数キーを取得(ベンチマークグリッドスナップショット項目 項目) =>
        項目.左.ベンチマーク?.引数キー??項目.中央?.ベンチマーク?.引数キー??項目.追加?.ベンチマーク?.引数キー??項目.右.ベンチマーク?.引数キー??string.Empty;

    private static string スナップショット行キーを作成(string 型名,string ベンチマーク名,string 引数キー) =>
        型名+"|"+ベンチマーク名+"|"+引数キー;

    private void スナップショット診断を記録(ベンチマークグリッドスナップショット項目 項目,string 理由) {
        起動診断.書き込む(
            "ベンチマークスナップショット読込診断: "+理由
            +" 実行ID="+項目.実行ID.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" ソースタブID="+項目.ソースタブID
            +" 型="+項目.型名
            +" ベンチマーク="+項目.ベンチマーク名);
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
    /// 終了ジョブか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ジョブ">ジョブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 終了ジョブか(ベンチマークジョブ ジョブ) =>
        ジョブ.状態 is ジョブ状態.完了 or ジョブ状態.失敗;

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
    /// 今回実行メモリ表示を維持するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool 今回実行メモリ表示を維持するか() =>
        ベンチマーク実行表示元ロジック.今回実行メモリ表示を維持するか(
            this.現在実行セッション.結果ありか);

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
    /// 要素数の現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の現在実行比較行を構築(int 要素数) {
        return this.要素数の現在実行比較行を構築(this.選択ソース定義,要素数);
    }

    /// <summary>
    /// 要素数の現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の現在実行比較行を構築(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        return this.実行投影サービス.要素数の比較行を構築(
            this.現在実行結果項目一覧を取得(ソースタブ.タブ),
            ソースタブ,
            要素数,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 現在実行比較行を構築() {
        return this.現在実行比較行を構築(this.選択ソース定義);
    }

    /// <summary>
    /// 現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 現在実行比較行を構築(ベンチマークソースタブ定義 ソースタブ) {
        return this.実行投影サービス.最新比較行を構築(
            this.現在実行結果項目一覧を取得(ソースタブ.タブ),
            ソースタブ,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 要素数の比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の比較行を構築(int 要素数) {
        return this.要素数の比較行を構築(this.選択ソース定義,要素数);
    }

    /// <summary>
    /// 要素数の比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の比較行を構築(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        var 現在実行結果一覧 = this.現在実行結果項目一覧を取得(ソースタブ.タブ);
        var ソース結果一覧 = 現在実行結果一覧
            .Where(結果 => 結果.軸数==要素数
                &&結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var 行一覧 = this.比較用報告行を作成(ソースタブ,ソース結果一覧);
        return 行一覧.Length==0
            ? []
            : ベンチマーク比較フォーマッター.比較行を構築(行一覧,ソースタブ,要素数);
    }

    /// <summary>
    /// 要素数の表示行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の表示行を構築(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        return this.実行投影サービス.選択要素数の表示行を構築(
            this.結果項目一覧.ToArray(),
            ソースタブ,
            要素数,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 表示行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 表示行を構築(ベンチマークソースタブ定義 ソースタブ) {
        return this.実行投影サービス.表示行を構築(
            this.結果項目一覧,
            ソースタブ,
            this.比較用報告行を作成);
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
            });
        }

        return 結果一覧;
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

    /// <summary>
    /// IReadOnlyList< メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="左プロジェクト">左プロジェクト を指定して実行条件を制御します。</param>
    /// <param name="右プロジェクト">右プロジェクト を指定して実行条件を制御します。</param>
    private IReadOnlyList<(string 左プロジェクト,string 右プロジェクト)> ベンチマークペア一覧を取得() =>
        ベンチマークペア一覧を作成(this.選択ソース定義);

    private static IReadOnlyList<(string 左プロジェクト,string 右プロジェクト)> ベンチマークペア一覧を作成(ベンチマークソースタブ定義 ソースタブ) =>
        ソースタブ.比較ペア一覧
            .Select(static ペア=>(ペア.左プロジェクト,ペア.右プロジェクト))
            .ToArray();


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

    private static ジョブ状態 再開用ジョブ状態へ変換(UI実行状態 状態) =>
        状態 switch {
            UI実行状態.完了 => ジョブ状態.完了,
            UI実行状態.失敗 => ジョブ状態.失敗,
            _ => ジョブ状態.保留,
        };

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
    /// 比較行が一致するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="項目">項目 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 比較行が一致するか(ベンチマーク表行 行,ベンチマーク比較行 項目) =>
        ベンチマーク比較行一致ロジック.一致か(
            行.メソッド名,
            行.ベンチマーク名,
            行.ソースタブ.タブ,
            行.プロジェクト別ケース.Values.Select(static ベンチマーク => new ベンチマーク比較行一致ケース(ベンチマーク.型完全名,ベンチマーク.引数キー)),
            項目);

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
        if(!string.IsNullOrWhiteSpace(this.選択ソース定義.中央プロジェクト)) {
            yield return ベンチマークグリッドソート列.左時間シェア;
            yield return ベンチマークグリッドソート列.左メモリシェア;
            yield return ベンチマークグリッドソート列.中央日時;
            yield return ベンチマークグリッドソート列.中央時間;
            yield return ベンチマークグリッドソート列.中央割り当て;
            if(this.選択ソース定義.比較ペア一覧.Any(ペア=>ペア.最適化列キー=="中央")){
                yield return ベンチマークグリッドソート列.中央時間シェア;
                yield return ベンチマークグリッドソート列.中央メモリシェア;
            }
        }
        if(!string.IsNullOrWhiteSpace(this.選択ソース定義.追加プロジェクト)) {
            yield return ベンチマークグリッドソート列.追加日時;
            yield return ベンチマークグリッドソート列.追加時間;
            yield return ベンチマークグリッドソート列.追加割り当て;
            if(this.選択ソース定義.比較ペア一覧.Any(ペア=>ペア.最適化列キー=="追加")){
                yield return ベンチマークグリッドソート列.追加時間シェア;
                yield return ベンチマークグリッドソート列.追加メモリシェア;
            }
        }
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

    /// <summary>
    /// 現 在 の 状 態 を 再 構 築 し 、 表 示 に 反 映 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void CPU区分を再構築() {
        this.CPU区分一覧.Clear();
        this.CPU区分を追加("Pコア",CPUコア種別.高性能);
        this.CPU区分を追加("Eコア",CPUコア種別.高効率);
        this.CPU区分を追加("不明コア",CPUコア種別.不明);
        this.OnPropertyChanged(nameof(this.高性能CPU区分一覧));
        this.OnPropertyChanged(nameof(this.高効率CPU区分一覧));
        this.OnPropertyChanged(nameof(this.不明CPU区分一覧));
    }

    /// <summary>
    /// CPU区分を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="種別">種別 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<CPU表区分> CPU区分を取得(CPUコア種別 種別) =>
        this.CPU区分一覧.Where(区分 => 区分.コア種別==種別);

    /// <summary>
    /// CPU区分を追加 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="タイトル">タイトル を指定して実行条件を制御します。</param>
    /// <param name="種別">種別 を指定して実行条件を制御します。</param>
    private void CPU区分を追加(string タイトル,CPUコア種別 種別) {
        var グループ一覧 = this.CPU項目一覧
            .Where(CPU => CPU.コア種別==種別)
            .GroupBy(static CPU => CPU.物理コアインデックス)
            .OrderBy(static グループ => グループ.Min(static CPU => CPU.論理プロセッサインデックス))
            .Select(static グループ => グループ.OrderBy(static CPU => CPU.論理プロセッサインデックス).ToArray())
            .Where(static グループ => グループ.Length>0)
            .ToArray();
        if(グループ一覧.Length==0) {
            return;
        }

        var 列数 = グループ一覧.Max(static グループ => グループ.Length);
        var 列一覧 = global::System.Linq.Enumerable.Range(0,列数)
            .Select(static インデックス => new CPU表列(インデックス,"選択"))
            .ToArray();
        var 行一覧 = グループ一覧
            .Select(グループ => new CPU表行(
                CPU範囲ラベルを作成(グループ),
                グループ[0].コア種別,
                グループ.Select(static CPU => new CPU表セル(CPU)).ToArray(),
                列数))
            .ToArray();
        this.CPU区分一覧.Add(new CPU表区分(タイトル,種別,列一覧,行一覧));
    }

    /// <summary>
    /// CPU範囲ラベルを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="CPU一覧">CPU一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CPU範囲ラベルを作成(IReadOnlyList<CPU選択項目> CPU一覧) {
        var 最小 = CPU一覧.Min(static CPU => CPU.論理プロセッサインデックス);
        var 最大 = CPU一覧.Max(static CPU => CPU.論理プロセッサインデックス);
        return 最小==最大
            ? 最小.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : 最小.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+最大.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// CPU選択を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="CPU一覧">CPU一覧 を指定して実行条件を制御します。</param>
    /// <param name="選択中か">選択中か を指定して実行条件を制御します。</param>
    private void CPU選択を適用(IEnumerable<CPU選択項目> CPU一覧,bool 選択中か) {
        if(this.更新中か&&!this.CPU選択適用中か) {
            return;
        }

        var 対象一覧 = CPU一覧.Distinct().ToArray();
        if(対象一覧.Length==0) {
            return;
        }

        this.CPU選択適用中か=true;
        try {
            foreach(var CPU in 対象一覧) {
                CPU.選択中か=選択中か;
            }
        } finally {
            this.CPU選択適用中か=false;
            this.選択論理CPUを更新();
            this.CPU区分状態を更新();
        }
    }

    /// <summary>
    /// 選択論理CPUを取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<int> 選択論理CPUを取得() =>
        Volatile.Read(ref this.選択論理CPU一覧);

    /// <summary>
    /// 選択論理CPUを更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択論理CPUを更新() {
        Volatile.Write(ref this.選択論理CPU一覧,this.選択論理CPUを作成());
        this.実行コマンド.NotifyCanExecuteChanged();
#if DEBUG
        this.無限実行コマンド.NotifyCanExecuteChanged();
#endif
        this.実行切替コマンド.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.ベンチマークを実行可能か));
        this.OnPropertyChanged(nameof(this.実行ツールバーボタン使用可能か));
        this.OnPropertyChanged(nameof(this.ツールバー状態入力));
    }

    /// <summary>
    /// 選択論理CPUを作成 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private int[] 選択論理CPUを作成() =>
        this.CPU項目一覧
            .Where(static CPU => CPU.選択中か)
            .Select(static CPU => CPU.論理プロセッサインデックス)
            .ToArray();

    /// <summary>
    /// CPU区分状態を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void CPU区分状態を更新() {
        foreach(var 区分 in this.CPU区分一覧) {
            区分.状態を更新();
            foreach(var 列 in 区分.列一覧) {
                this.OnPropertyChanged("CPU列状態"+区分.タイトル+列.インデックス);
            }
        }
    }

}
