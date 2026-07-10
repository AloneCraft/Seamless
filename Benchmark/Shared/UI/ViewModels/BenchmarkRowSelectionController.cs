using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.GridCommon.ScrollAxis;

namespace UI;

internal sealed class BenchmarkRowSelectionController:DataGridRowSelectionController{
    private readonly Action<IReadOnlyList<BenchmarkTableRow>> toggleRows;
    private readonly Action<IReadOnlyList<BenchmarkTableRow>,IReadOnlyList<BenchmarkTableRow>> synchronizeSelection;
    private readonly Func<IEnumerable<BenchmarkTableRow>> getRows;
    private readonly Func<BenchmarkCellSelectionGesture> getPointerGesture;
    private readonly Dictionary<string,BenchmarkTableRow> selectedRowsByKey=new(StringComparer.OrdinalIgnoreCase);
    private BenchmarkTableRow? rangeAnchor;
    private BenchmarkTableRow? activeRow;
    private bool isUpdatingSelection;
    private bool isRebindPending;
    private bool isModifierPointerSelectionPending;
    private BenchmarkCellSelectionGesture pendingPointerGesture;

    public BenchmarkRowSelectionController(
        SfDataGrid dataGrid,
        Action<IReadOnlyList<BenchmarkTableRow>> toggleRows,
        Action<IReadOnlyList<BenchmarkTableRow>,IReadOnlyList<BenchmarkTableRow>> synchronizeSelection,
        Func<IEnumerable<BenchmarkTableRow>> getRows,
        Func<BenchmarkCellSelectionGesture> getPointerGesture)
        : base(dataGrid){
        this.toggleRows=toggleRows;
        this.synchronizeSelection=synchronizeSelection;
        this.getRows=getRows;
        this.getPointerGesture=getPointerGesture;
    }

    internal void HandleSelectionChanged(DataGridSelectionChangedEventArgs args){
        if(this.isUpdatingSelection){
            return;
        }

        if(this.isModifierPointerSelectionPending){
            return;
        }

        var addedRows=GetSelectedRows(args.AddedRows).ToArray();
        var removedRows=GetSelectedRows(args.RemovedRows).ToArray();
        this.ApplyTrackedSelectionDelta(addedRows,removedRows);
        var currentRow=GetRow(this.DataGrid?.CurrentRow);
        if(currentRow is null||!this.selectedRowsByKey.ContainsKey(currentRow.RowKey)){
            currentRow=addedRows.LastOrDefault();
        }
        if(currentRow is not null&&this.selectedRowsByKey.ContainsKey(currentRow.RowKey)){
            this.activeRow=currentRow;
            if(!this.IsShiftKeyPressed){
                this.rangeAnchor=currentRow;
            }
        }else if(this.selectedRowsByKey.Count==0){
            this.activeRow=null;
            this.rangeAnchor=null;
        }

        this.synchronizeSelection(addedRows,removedRows);
    }

    internal void CompletePointerSelection(BenchmarkTableRow row){
        if(!this.isModifierPointerSelectionPending){
            return;
        }

        var gesture=this.pendingPointerGesture;
        this.isModifierPointerSelectionPending=false;
        this.pendingPointerGesture=BenchmarkCellSelectionGesture.Replace;
        this.SelectRow(row,gesture);
    }

    internal static IEnumerable<BenchmarkTableRow> GetSelectedRows(System.Collections.IEnumerable? selectedRows){
        if(selectedRows is null){
            return [];
        }

        return selectedRows
            .Cast<object>()
            .Select(GetRow)
            .OfType<BenchmarkTableRow>();
    }

    internal static BenchmarkTableRow? GetRow(object? row) => row switch {
        BenchmarkTableRow benchmarkRow => benchmarkRow,
        DataGridRowInfo { RowData: BenchmarkTableRow benchmarkRow } => benchmarkRow,
        DataRowBase { RowData: BenchmarkTableRow benchmarkRow } => benchmarkRow,
        _ => null,
    };

    internal static IReadOnlyList<BenchmarkTableRow> ResolveToggleSelection(
        IEnumerable<BenchmarkTableRow> selectedRows,
        BenchmarkTableRow row){
        var resolvedRows=selectedRows.ToList();
        var selectedIndex=resolvedRows.FindIndex(item => string.Equals(item.RowKey,row.RowKey,StringComparison.OrdinalIgnoreCase));
        if(selectedIndex>=0){
            resolvedRows.RemoveAt(selectedIndex);
        }else{
            resolvedRows.Add(row);
        }

        return resolvedRows;
    }

    internal static IReadOnlyList<BenchmarkTableRow> ResolveRangeSelection(
        IReadOnlyList<BenchmarkTableRow> rows,
        BenchmarkTableRow anchor,
        BenchmarkTableRow row){
        var anchorIndex=GetIndex(rows,anchor);
        var rowIndex=GetIndex(rows,row);
        if(anchorIndex<0||rowIndex<0){
            return [];
        }

        var start=Math.Min(anchorIndex,rowIndex);
        var count=Math.Abs(anchorIndex-rowIndex)+1;
        return rows.Skip(start).Take(count).ToArray();
    }

    internal void SelectAll(IEnumerable<BenchmarkTableRow> rows){
        var selectedRows=rows.ToArray();
        this.ReplaceSelection(selectedRows,selectedRows.LastOrDefault(),selectedRows.FirstOrDefault());
    }

    internal void RestoreSelection(IEnumerable<string> rowKeys){
        var selectedRowKeys=rowKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(selectedRowKeys.Count==0){
            return;
        }

        var selectedRows=this.getRows()
            .Where(row => selectedRowKeys.Contains(row.RowKey))
            .ToArray();
        this.ReplaceSelection(selectedRows,selectedRows.LastOrDefault(),selectedRows.FirstOrDefault());
    }

    internal void QueueRebindSelection(){
        if(this.isRebindPending||this.DataGrid is not { } dataGrid){
            return;
        }

        this.isRebindPending=true;
        dataGrid.Dispatcher.Dispatch(() => {
            this.isRebindPending=false;
            var rows=this.getRows().ToArray();
            var selectedRows=rows
                .Where(row => this.selectedRowsByKey.ContainsKey(row.RowKey))
                .ToArray();
            var activeRow=this.FindCurrentRow(rows,this.activeRow);
            var rangeAnchor=this.FindCurrentRow(rows,this.rangeAnchor);
            this.ReplaceSelection(selectedRows,activeRow,rangeAnchor);
        });
    }

    internal void SelectRow(BenchmarkTableRow row,BenchmarkCellSelectionGesture gesture){
        if(gesture==BenchmarkCellSelectionGesture.Toggle){
            var selectedRows=ResolveToggleSelection(this.selectedRowsByKey.Values,row);
            this.ReplaceSelection(selectedRows,row,row);
            return;
        }

        if(gesture is BenchmarkCellSelectionGesture.Extend or BenchmarkCellSelectionGesture.ExtendToggle){
            var rows=this.getRows().ToArray();
            if(rows.Length==0){
                return;
            }

            var anchor=this.FindCurrentRow(rows,this.rangeAnchor)??this.FindCurrentRow(rows,this.activeRow)??row;
            var selectedRows=ResolveRangeSelection(rows,anchor,row);
            if(selectedRows.Count>0){
                this.ReplaceSelection(selectedRows,row,anchor);
            }

            return;
        }

        this.ReplaceSelection([row],row,row);
    }

    protected override void ProcessKeyDown(KeyEventArgs args,bool isCtrlKeyPressed,bool isShiftKeyPressed){
        if(args.Key==KeyboardKey.Space){
            var selectedRows=this.selectedRowsByKey.Values.ToArray();
            if(selectedRows.Length>0){
                this.toggleRows(selectedRows);
            }

            args.Handled=true;
            return;
        }

        if(args.Key is not (KeyboardKey.Up or KeyboardKey.Down)||isCtrlKeyPressed){
            base.ProcessKeyDown(args,isCtrlKeyPressed,isShiftKeyPressed);
            return;
        }

        var rows=this.getRows().ToArray();
        if(rows.Length==0||this.DataGrid is not { } dataGrid){
            args.Handled=true;
            return;
        }

        var direction=args.Key==KeyboardKey.Down ? 1 : -1;
        if(isShiftKeyPressed){
            this.ExtendSelection(dataGrid,rows,direction);
        }else{
            this.MoveSelection(dataGrid,rows,direction);
        }

        args.Handled=true;
    }

    protected override void ProcessPointerPressed(Syncfusion.Maui.Core.Internals.PointerEventArgs args,RowColumnIndex rowColumnIndex){
        this.pendingPointerGesture=this.getPointerGesture();
        this.isModifierPointerSelectionPending=this.pendingPointerGesture!=BenchmarkCellSelectionGesture.Replace;
        base.ProcessPointerPressed(args,rowColumnIndex);
    }

    private void MoveSelection(SfDataGrid dataGrid,IReadOnlyList<BenchmarkTableRow> rows,int direction){
        var current=this.FindCurrentRow(rows,this.activeRow);
        var currentIndex=current is null ? (direction>0 ? -1 : rows.Count) : GetIndex(rows,current);
        var nextIndex=Math.Clamp(currentIndex+direction,0,rows.Count-1);
        var nextRow=rows[nextIndex];
        this.ReplaceSelection([nextRow],nextRow,nextRow);
        dataGrid.ScrollToRowIndex(nextIndex+2,ScrollToPosition.MakeVisible,false);
    }

    private void ExtendSelection(SfDataGrid dataGrid,IReadOnlyList<BenchmarkTableRow> rows,int direction){
        var anchor=this.FindCurrentRow(rows,this.rangeAnchor)
            ??rows.FirstOrDefault(row => this.selectedRowsByKey.ContainsKey(row.RowKey));
        if(anchor is null){
            return;
        }

        var active=this.FindCurrentRow(rows,this.activeRow);
        if(active is null){
            var selected=rows.Where(row => this.selectedRowsByKey.ContainsKey(row.RowKey));
            active=direction>0
                ? selected.OrderBy(row => GetIndex(rows,row)).LastOrDefault()
                : selected.OrderBy(row => GetIndex(rows,row)).FirstOrDefault();
        }

        var nextIndex=Math.Clamp(GetIndex(rows,active??anchor)+direction,0,rows.Count-1);
        var anchorIndex=GetIndex(rows,anchor);
        var start=Math.Min(anchorIndex,nextIndex);
        var count=Math.Abs(anchorIndex-nextIndex)+1;
        this.ReplaceSelection(rows.Skip(start).Take(count),rows[nextIndex],anchor);
        dataGrid.ScrollToRowIndex(nextIndex+2,ScrollToPosition.MakeVisible,false);
    }

    private void ReplaceSelection(
        IEnumerable<BenchmarkTableRow> rows,
        BenchmarkTableRow? activeRow,
        BenchmarkTableRow? rangeAnchor){
        if(this.DataGrid is not { } dataGrid){
            return;
        }

        var selectedRows=rows
            .GroupBy(static row => row.RowKey,StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .ToArray();
        var nextRowsByKey=selectedRows.ToDictionary(static row => row.RowKey,StringComparer.OrdinalIgnoreCase);
        var removedRows=this.selectedRowsByKey
            .Where(item => !nextRowsByKey.TryGetValue(item.Key,out var next)||!ReferenceEquals(item.Value,next))
            .Select(static item => item.Value)
            .ToArray();
        var addedRows=nextRowsByKey
            .Where(item => !this.selectedRowsByKey.TryGetValue(item.Key,out var previous)||!ReferenceEquals(previous,item.Value))
            .Select(static item => item.Value)
            .ToArray();

        this.isUpdatingSelection=true;
        try{
            dataGrid.SelectedRows.Clear();
            foreach(var row in selectedRows){
                dataGrid.SelectedRows.Add(row);
            }
        }finally{
            this.isUpdatingSelection=false;
        }

        this.selectedRowsByKey.Clear();
        foreach(var row in selectedRows){
            this.selectedRowsByKey[row.RowKey]=row;
        }
        this.activeRow=activeRow;
        this.rangeAnchor=rangeAnchor;
        if(addedRows.Length>0||removedRows.Length>0){
            this.synchronizeSelection(addedRows,removedRows);
        }
    }

    private void ApplyTrackedSelectionDelta(
        IReadOnlyList<BenchmarkTableRow> addedRows,
        IReadOnlyList<BenchmarkTableRow> removedRows){
        foreach(var row in removedRows){
            this.selectedRowsByKey.Remove(row.RowKey);
        }
        foreach(var row in addedRows){
            this.selectedRowsByKey[row.RowKey]=row;
        }
    }

    private BenchmarkTableRow? FindCurrentRow(
        IReadOnlyList<BenchmarkTableRow> rows,
        BenchmarkTableRow? row){
        if(row is null){
            return null;
        }

        return rows.FirstOrDefault(item => string.Equals(item.RowKey,row.RowKey,StringComparison.OrdinalIgnoreCase));
    }

    private static int GetIndex(IReadOnlyList<BenchmarkTableRow> rows,BenchmarkTableRow row){
        for(var index=0;index<rows.Count;index++){
            if(ReferenceEquals(rows[index],row)){
                return index;
            }
        }

        return -1;
    }
}
