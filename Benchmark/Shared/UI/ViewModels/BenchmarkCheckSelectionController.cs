using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;

namespace UI;

/// <summary>
/// SfDataGrid の標準行選択へ、チェック操作と選択差分通知だけを追加します。
/// </summary>
internal sealed class ベンチマークチェック選択コントローラー:DataGridRowSelectionController {
    private readonly グリッドチェック選択動作<ベンチマーク表行> チェック選択;
    private readonly Action<IReadOnlyList<ベンチマーク表行>,IReadOnlyList<ベンチマーク表行>> 選択を同期;

    public ベンチマークチェック選択コントローラー(
        SfDataGrid データグリッド,
        Action<IReadOnlyList<ベンチマーク表行>> 行を切り替える,
        Action<IReadOnlyList<ベンチマーク表行>,IReadOnlyList<ベンチマーク表行>> 選択を同期)
        : base(データグリッド) {
        this.チェック選択=new グリッドチェック選択動作<ベンチマーク表行>(
            () => 選択行を取得(データグリッド.SelectedRows).ToArray(),
            行を切り替える);
        this.選択を同期=選択を同期;
    }

    internal void 選択変更を処理(DataGridSelectionChangedEventArgs 引数) {
        var 追加行 = 選択行を取得(引数.AddedRows).ToArray();
        var 削除行 = 選択行を取得(引数.RemovedRows).ToArray();
        if(追加行.Length!=0||削除行.Length!=0) {
            this.選択を同期(追加行,削除行);
        }
        this.キーボードフォーカスを予約();
    }

    internal static IEnumerable<ベンチマーク表行> 選択行を取得(System.Collections.IEnumerable? 選択行) {
        return 選択行 is null
            ? []
            : 選択行.Cast<object>().Select(行を取得).OfType<ベンチマーク表行>();
    }

    internal void 単一選択(ベンチマーク表行 行) {
        if(this.DataGrid is { } データグリッド) {
            データグリッド.SelectedRows.Clear();
            データグリッド.SelectedRows.Add(行);
            データグリッド.CurrentRow=行;
            this.キーボードフォーカスを予約();
        }
    }

    internal void キーボードフォーカスを予約() {
        if(this.DataGrid is not { } データグリッド) {
            return;
        }
        グリッドキーボードフォーカス動作.フォーカスを予約(データグリッド);
    }

    protected override void ProcessKeyDown(KeyEventArgs 引数,bool コントロールキー押下か,bool シフトキー押下か) {
        if(this.チェック選択.キー押下を処理(引数,コントロールキー押下か,シフトキー押下か)) {
            return;
        }
        base.ProcessKeyDown(引数,コントロールキー押下か,シフトキー押下か);
    }

    private static ベンチマーク表行? 行を取得(object? 行) => 行 switch {
        ベンチマーク表行 ベンチマーク行 => ベンチマーク行,
        DataGridRowInfo { RowData: ベンチマーク表行 ベンチマーク行 } => ベンチマーク行,
        DataRowBase { RowData: ベンチマーク表行 ベンチマーク行 } => ベンチマーク行,
        _ => null,
    };
}
