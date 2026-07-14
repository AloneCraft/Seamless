using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;

using System.Runtime.CompilerServices;

namespace UI;

internal sealed class グリッドチェック選択動作<T項目> {
    private readonly Func<IReadOnlyList<T項目>> 選択対象を取得;
    private readonly Action<IReadOnlyList<T項目>> チェックを切り替える;

    public グリッドチェック選択動作(
        Func<IReadOnlyList<T項目>> 選択対象を取得,
        Action<IReadOnlyList<T項目>> チェックを切り替える) {
        this.選択対象を取得=選択対象を取得;
        this.チェックを切り替える=チェックを切り替える;
    }

    public bool キー押下を処理(KeyEventArgs 引数,bool コントロールキー押下か,bool シフトキー押下か) {
        if(!グリッドチェック選択ロジック.チェックを切り替えるべきか(引数.Key,コントロールキー押下か,シフトキー押下か)) {
            return false;
        }

        var 対象 = this.選択対象を取得();
        if(対象.Count!=0) {
            this.チェックを切り替える(対象);
        }
        引数.Handled=true;
        return true;
    }
}

internal static class グリッドチェック選択ロジック {
    public static bool チェックを切り替えるべきか(KeyboardKey キー,bool コントロールキー押下か,bool シフトキー押下か) =>
        キー==KeyboardKey.Space&&!コントロールキー押下か&&!シフトキー押下か;
}

internal static class グリッドキーボードフォーカス動作 {
    private static readonly ConditionalWeakTable<SfDataGrid,PointerGestureRecognizer> フォーカスポインター = new();

    public static void 取り付ける(SfDataGrid データグリッド) {
        フォーカスポインター.GetValue(データグリッド,static グリッド => {
            var ポインター = new PointerGestureRecognizer();
            ポインター.PointerReleased+=(_,_) => フォーカスを予約(グリッド);
            グリッド.GestureRecognizers.Add(ポインター);
            グリッド.SelectionChanged+=(_,_) => フォーカスを予約(グリッド);
            return ポインター;
        });
    }

    public static void フォーカスを予約(SfDataGrid データグリッド) {
        データグリッド.Dispatcher.Dispatch(() => データグリッド.GetVisualContainer()?.Focus());
    }
}
