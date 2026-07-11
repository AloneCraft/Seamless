using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.GridCommon.ScrollAxis;

namespace UI;

/// <summary>
/// SfDataGrid の標準行選択へ、チェック操作と選択差分通知だけを追加します。
/// </summary>
internal sealed class BenchmarkCheckSelectionController:DataGridRowSelectionController{
    private readonly Action<IReadOnlyList<BenchmarkTableRow>> toggleRows;
    private readonly Action<IReadOnlyList<BenchmarkTableRow>,IReadOnlyList<BenchmarkTableRow>> synchronizeSelection;
    private readonly Func<IEnumerable<BenchmarkTableRow>> getRows;
    private readonly Dictionary<string,BenchmarkTableRow> selectedRowsByKey=new(StringComparer.OrdinalIgnoreCase);
    private bool isRestoringSelection;
    private bool isRebindPending;

    public BenchmarkCheckSelectionController(
        SfDataGrid dataGrid,
        Action<IReadOnlyList<BenchmarkTableRow>> toggleRows,
        Action<IReadOnlyList<BenchmarkTableRow>,IReadOnlyList<BenchmarkTableRow>> synchronizeSelection,
        Func<IEnumerable<BenchmarkTableRow>> getRows)
        :base(dataGrid){
        this.toggleRows=toggleRows;
        this.synchronizeSelection=synchronizeSelection;
        this.getRows=getRows;
    }

    internal void HandleSelectionChanged(DataGridSelectionChangedEventArgs args){
        if(this.isRestoringSelection){
            return;
        }
        var addedRows=GetSelectedRows(args.AddedRows).ToArray();
        var removedRows=GetSelectedRows(args.RemovedRows).ToArray();
        foreach(var row in removedRows){
            this.selectedRowsByKey.Remove(row.RowKey);
        }
        foreach(var row in addedRows){
            this.selectedRowsByKey[row.RowKey]=row;
        }
        if(addedRows.Length!=0||removedRows.Length!=0){
            this.synchronizeSelection(addedRows,removedRows);
        }
    }

    internal static IEnumerable<BenchmarkTableRow> GetSelectedRows(System.Collections.IEnumerable? selectedRows){
        return selectedRows is null
            ? []
            : selectedRows.Cast<object>().Select(GetRow).OfType<BenchmarkTableRow>();
    }

    internal void RestoreSelection(IEnumerable<string> selectedRowKeys){
        var keys=selectedRowKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        this.ApplyRestoredSelection(this.getRows().Where(row => keys.Contains(row.RowKey)).ToArray());
    }

    internal void QueueRebindSelection(){
        if(this.isRebindPending){
            return;
        }
        this.isRebindPending=true;
        var keys=this.selectedRowsByKey.Keys.ToArray();
        this.DataGrid?.Dispatcher.Dispatch(() => {
            this.isRebindPending=false;
            this.RestoreSelection(keys);
        });
    }

    internal void SelectSingle(BenchmarkTableRow row){
        this.ApplyRestoredSelection([row]);
        if(this.DataGrid is { } dataGrid){
            dataGrid.CurrentRow=row;
            dataGrid.GetVisualContainer()?.Focus();
        }
    }

    protected override void ProcessKeyDown(KeyEventArgs args,bool isCtrlKeyPressed,bool isShiftKeyPressed){
        if(args.Key==KeyboardKey.Space&&!isCtrlKeyPressed&&!isShiftKeyPressed){
            var rows=GetSelectedRows(this.DataGrid?.SelectedRows).ToArray();
            if(rows.Length!=0){
                this.toggleRows(rows);
            }
            args.Handled=true;
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

    private void ApplyRestoredSelection(IReadOnlyList<BenchmarkTableRow> rows){
        if(this.DataGrid is not { } dataGrid){
            return;
        }
        var previous=this.selectedRowsByKey.Values.ToArray();
        this.isRestoringSelection=true;
        try{
            dataGrid.SelectedRows.Clear();
            foreach(var row in rows){
                dataGrid.SelectedRows.Add(row);
            }
        }finally{
            this.isRestoringSelection=false;
        }
        this.selectedRowsByKey.Clear();
        foreach(var row in rows){
            this.selectedRowsByKey[row.RowKey]=row;
        }
        var currentByKey=rows.ToDictionary(static row => row.RowKey,StringComparer.OrdinalIgnoreCase);
        var previousByKey=previous.ToDictionary(static row => row.RowKey,StringComparer.OrdinalIgnoreCase);
        var removed=previous.Where(row => !currentByKey.TryGetValue(row.RowKey,out var current)||!ReferenceEquals(row,current)).ToArray();
        var added=rows.Where(row => !previousByKey.TryGetValue(row.RowKey,out var old)||!ReferenceEquals(row,old)).ToArray();
        if(added.Length!=0||removed.Length!=0){
            this.synchronizeSelection(added,removed);
        }
    }
}
