using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;

namespace UI;

/// <summary>
/// SfDataGrid の標準セル選択へ、修飾なしスペースキーのチェック操作だけを追加します。
/// </summary>
internal sealed class CPUチェック選択コントローラー(SfDataGrid データグリッド,Action<IReadOnlyList<CPU選択項目>> チェックを切り替える)
    :DataGridCellSelectionController(データグリッド) {
    private readonly グリッドチェック選択動作<CPU選択項目> チェック選択=new(
        () => 選択CPUを取得(データグリッド),
        チェックを切り替える);
    protected override void ProcessKeyDown(KeyEventArgs 引数,bool コントロールキー押下か,bool シフトキー押下か) {
        if(this.チェック選択.キー押下を処理(引数,コントロールキー押下か,シフトキー押下か)) {
            return;
        }
        base.ProcessKeyDown(引数,コントロールキー押下か,シフトキー押下か);
    }

    private static IReadOnlyList<CPU選択項目> 選択CPUを取得(SfDataGrid データグリッド) =>
        データグリッド.GetSelectedCells()
            .Select(CPUを取得)
            .Where(static CPU => CPU is not null)
            .Select(static CPU => CPU!)
            .DistinctBy(static CPU => CPU.論理プロセッサインデックス)
            .ToArray();

    internal static bool 選択可能なCPUセルか(SelectedCellInfo セル) => CPUを取得(セル) is not null;

    private static CPU選択項目? CPUを取得(SelectedCellInfo セル) {
        if(セル.RowData is not CPU表行 行||セル.Column is null||!CPU列番号の取得を試みる(セル.Column.MappingName,out var 列番号)) {
            return null;
        }
        return 列番号<行.グリッドセル一覧.Count ? 行.グリッドセル一覧[列番号]?.CPU : null;
    }

    private static bool CPU列番号の取得を試みる(string? マッピング名,out int 列番号) {
        const string 接頭辞 = "CPU";
        列番号=-1;
        return マッピング名 is not null
            &&マッピング名.StartsWith(接頭辞,StringComparison.Ordinal)
            &&int.TryParse(マッピング名.AsSpan(接頭辞.Length),out 列番号)
            &&列番号>=0;
    }
}
