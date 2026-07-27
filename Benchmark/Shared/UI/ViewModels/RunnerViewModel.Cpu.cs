using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// CPU の選択と区分
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// CPUトポロジーサービス フィールドです。
    /// </summary>
    private readonly ICPUトポロジーサービス CPUトポロジーサービス;

    /// <summary>
    /// CPU選択適用中か フィールドです。
    /// </summary>
    private bool CPU選択適用中か;

    /// <summary>
    /// 選択論理CPU一覧 フィールドです。
    /// </summary>
    private int[] 選択論理CPU一覧 = [];

    /// <summary>
    /// 選択下段タブ値 フィールドです。
    /// </summary>
    private ベンチマーク下段タブ 選択下段タブ値 = ベンチマーク下段タブ.CPU;

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

    private static int? CPU番号を解析(string 値) =>
        int.TryParse(値,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var 解析済み) ? 解析済み : null;

    /// <summary>
    /// CPUジョブをクリア メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void CPUジョブをクリア() {
        foreach(var CPU in this.CPU項目一覧) {
            CPU.ジョブをクリア();
        }
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

    private static int? スナップショット側CPU番号を解決(int? 行CPU番号,ベンチマークグリッドスナップショット側 側) =>
        側.結果項目.割り当て論理CPU??行CPU番号;

    private CPU選択項目? スナップショットCPUを検索(int? CPU番号) =>
        CPU番号 is { } 値
            ? this.CPU項目一覧.FirstOrDefault(項目 => 項目.論理プロセッサインデックス==値)
            : null;

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
