using System.Runtime.CompilerServices;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;

namespace UI;

internal sealed class GridCheckSelectionBehavior<T>{
    private readonly Func<IReadOnlyList<T>> getSelectedTargets;
    private readonly Action<IReadOnlyList<T>> toggleChecks;

    public GridCheckSelectionBehavior(
        Func<IReadOnlyList<T>> getSelectedTargets,
        Action<IReadOnlyList<T>> toggleChecks){
        this.getSelectedTargets=getSelectedTargets;
        this.toggleChecks=toggleChecks;
    }

    public bool ProcessKeyDown(KeyEventArgs args,bool isCtrlKeyPressed,bool isShiftKeyPressed){
        if(!GridCheckSelectionLogic.ShouldToggleChecks(args.Key,isCtrlKeyPressed,isShiftKeyPressed)){
            return false;
        }

        var targets=this.getSelectedTargets();
        if(targets.Count!=0){
            this.toggleChecks(targets);
        }
        args.Handled=true;
        return true;
    }
}

internal static class GridCheckSelectionLogic{
    public static bool ShouldToggleChecks(KeyboardKey key,bool isCtrlKeyPressed,bool isShiftKeyPressed) =>
        key==KeyboardKey.Space&&!isCtrlKeyPressed&&!isShiftKeyPressed;
}

internal static class GridKeyboardFocusBehavior{
    private static readonly ConditionalWeakTable<SfDataGrid,PointerGestureRecognizer> focusPointers=new();

    public static void Attach(SfDataGrid dataGrid){
        focusPointers.GetValue(dataGrid,static grid => {
            var pointer=new PointerGestureRecognizer();
            pointer.PointerReleased+=(_,_) => QueueFocus(grid);
            grid.GestureRecognizers.Add(pointer);
            grid.SelectionChanged+=(_,_) => QueueFocus(grid);
            return pointer;
        });
    }

    public static void QueueFocus(SfDataGrid dataGrid){
        dataGrid.Dispatcher.Dispatch(() => dataGrid.GetVisualContainer()?.Focus());
    }
}
