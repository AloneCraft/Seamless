using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;

namespace UI;

/// <summary>
/// SfDataGrid の標準セル選択へ、修飾なしスペースキーのチェック操作だけを追加します。
/// </summary>
internal sealed class CPUチェック選択コントローラー(SfDataGrid データグリッド,Action<IReadOnlyList<CPU選択項目>> CPU一覧を切り替える)
    :DataGridCellSelectionController(データグリッド) {
    private readonly SfDataGrid グリッド=データグリッド;
    private readonly グリッドチェック選択動作<CPU選択項目> チェック選択=new(
        () => 選択CPUを取得(データグリッド.GetSelectedCells()),
        CPU一覧を切り替える);

    internal void チェッククリックを処理(CPU表行 行,int 列インデックス,IReadOnlyList<SelectedCellInfo>? 選択スナップショット) {
        this.チェッククリックを処理(行,"CPU"+列インデックス,選択スナップショット);
    }

    internal void コアチェッククリックを処理(CPU表行 行,IReadOnlyList<SelectedCellInfo>? 選択スナップショット) {
        this.チェッククリックを処理(行,nameof(CPU表行.ラベル),選択スナップショット);
    }

    protected override void ProcessKeyDown(KeyEventArgs 引数,bool コントロールキー押下か,bool シフトキー押下か) {
        if(this.チェック選択.キー押下を処理(引数,コントロールキー押下か,シフトキー押下か)) {
            return;
        }
        base.ProcessKeyDown(引数,コントロールキー押下か,シフトキー押下か);
    }

    internal static bool 選択可能なCPUセルか(SelectedCellInfo セル) => CPUを取得(セル).Any();

    private void チェッククリックを処理(CPU表行 行,string マッピング名,IReadOnlyList<SelectedCellInfo>? 選択スナップショット) {
        var クリックCPU一覧=CPUを取得(行,マッピング名)
            .DistinctBy(static CPU => CPU.論理プロセッサインデックス)
            .ToArray();
        if(クリックCPU一覧.Length==0) {
            return;
        }
        var 選択セル一覧=選択スナップショット??this.グリッド.GetSelectedCells();
        var 対象CPU一覧=選択セルか(選択セル一覧,行,マッピング名)
            ? 選択CPUを取得(選択セル一覧)
            : クリックCPU一覧;
        this.チェック選択を実行(対象CPU一覧);
    }

    private void チェック選択を実行(IReadOnlyList<CPU選択項目> CPU一覧) {
        if(CPU一覧.Count==0) {
            return;
        }
        CPU一覧を切り替える(CPU一覧);
        グリッドキーボードフォーカス動作.フォーカスを予約(this.グリッド);
    }

    private static IReadOnlyList<CPU選択項目> 選択CPUを取得(IEnumerable<SelectedCellInfo> 選択セル一覧) =>
        選択セル一覧
            .SelectMany(CPUを取得)
            .DistinctBy(static CPU => CPU.論理プロセッサインデックス)
            .ToArray();

    private static bool 選択セルか(IEnumerable<SelectedCellInfo> 選択セル一覧,CPU表行 行,string マッピング名) {
        return 選択セル一覧.Any(セル =>
            ReferenceEquals(セル.RowData,行)
            &&string.Equals(セル.Column?.MappingName,マッピング名,StringComparison.Ordinal));
    }

    private static IEnumerable<CPU選択項目> CPUを取得(SelectedCellInfo セル) {
        if(セル.RowData is not CPU表行 行||セル.Column is null) {
            return [];
        }
        return CPUを取得(行,セル.Column.MappingName);
    }

    private static IEnumerable<CPU選択項目> CPUを取得(CPU表行 行,string? マッピング名) {
        if(string.Equals(マッピング名,nameof(CPU表行.ラベル),StringComparison.Ordinal)) {
            return 行.セル一覧.Select(static セル => セル.CPU);
        }
        if(!CPU列番号の取得を試みる(マッピング名,out var 列番号)||列番号>=行.グリッドセル一覧.Count) {
            return [];
        }
        return 行.グリッドセル一覧[列番号] is { } セル ? [セル.CPU] : [];
    }

    private static bool CPU列番号の取得を試みる(string? マッピング名,out int 列番号) {
        const string 接頭辞="CPU";
        列番号=-1;
        return マッピング名 is not null
            &&マッピング名.StartsWith(接頭辞,StringComparison.Ordinal)
            &&int.TryParse(マッピング名.AsSpan(接頭辞.Length),out 列番号)
            &&列番号>=0;
    }
}
