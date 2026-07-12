using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.GridCommon.ScrollAxis;

namespace UI;

/// <summary>
/// SfDataGrid の標準行選択へ、チェック操作と選択差分通知だけを追加します。
/// </summary>
internal sealed class BenchmarkCheckSelectionController:DataGridRowSelectionController{
    private readonly GridCheckSelectionBehavior<BenchmarkTableRow> checkSelection;
    private readonly Action<IReadOnlyList<BenchmarkTableRow>,IReadOnlyList<BenchmarkTableRow>> synchronizeSelection;

    public BenchmarkCheckSelectionController(
        SfDataGrid dataGrid,
        Action<IReadOnlyList<BenchmarkTableRow>> toggleRows,
        Action<IReadOnlyList<BenchmarkTableRow>,IReadOnlyList<BenchmarkTableRow>> synchronizeSelection)
        :base(dataGrid){
        this.checkSelection=new GridCheckSelectionBehavior<BenchmarkTableRow>(
            () => GetSelectedRows(dataGrid.SelectedRows).ToArray(),
            toggleRows);
        this.synchronizeSelection=synchronizeSelection;
    }

    internal void HandleSelectionChanged(DataGridSelectionChangedEventArgs args){
        var addedRows=GetSelectedRows(args.AddedRows).ToArray();
        var removedRows=GetSelectedRows(args.RemovedRows).ToArray();
        if(addedRows.Length!=0||removedRows.Length!=0){
            this.synchronizeSelection(addedRows,removedRows);
        }
        this.QueueKeyboardFocus();
    }

    internal static IEnumerable<BenchmarkTableRow> GetSelectedRows(System.Collections.IEnumerable? selectedRows){
        return selectedRows is null
            ? []
            : selectedRows.Cast<object>().Select(GetRow).OfType<BenchmarkTableRow>();
    }

    internal void SelectSingle(BenchmarkTableRow row){
        if(this.DataGrid is { } dataGrid){
            dataGrid.SelectedRows.Clear();
            dataGrid.SelectedRows.Add(row);
            dataGrid.CurrentRow=row;
            this.QueueKeyboardFocus();
        }
    }

    internal void QueueKeyboardFocus(){
        if(this.DataGrid is not { } dataGrid){
            return;
        }
        GridKeyboardFocusBehavior.QueueFocus(dataGrid);
    }

    protected override void ProcessKeyDown(KeyEventArgs args,bool isCtrlKeyPressed,bool isShiftKeyPressed){
        if(this.checkSelection.ProcessKeyDown(args,isCtrlKeyPressed,isShiftKeyPressed)){
            return;
        }
        base.ProcessKeyDown(args,isCtrlKeyPressed,isShiftKeyPressed);
    }

    private static BenchmarkTableRow? GetRow(object? row) => row switch{
        BenchmarkTableRow benchmarkRow => benchmarkRow,
        DataGridRowInfo { RowData: BenchmarkTableRow benchmarkRow } => benchmarkRow,
        DataRowBase { RowData: BenchmarkTableRow benchmarkRow } => benchmarkRow,
        _ => null,
    };
}
