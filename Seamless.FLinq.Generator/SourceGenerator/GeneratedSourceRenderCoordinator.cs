namespace Seamless.FLinq.Generator;

/// <summary>
/// 入力ファイル単位のRender入力を、診断・trace付きのRenderer結果へ変換するCoordinatorです。
/// 既存のソース構築アルゴリズムは互換アダプターとしてGenerator partial側に残し、
/// 入出力の境界だけをこの型で固定します。
/// </summary>
internal static class GeneratedSourceRenderCoordinator {
    internal static GeneratedSourceRenderResult Render(GeneratedSourceRenderModel model) {
        var diagnostics=new List<Diagnostic>();
        var source=FLinqIncrementalGenerator.インターセプターソース構築(
            model.Targets,
            diagnostics,
            out var traceDocuments,
            out var planDrafts);
        // 目印は計測版の組み立てで使い終わっている。ほかの経路に残った目印も出力しない。
        var annotated=GeneratedExecutionAnnotations.Apply(QueryProbeMarkers.Strip(source));
        var completed=GeneratedControlFlowCapture.Complete(annotated,model.Targets,planDrafts);
        return new GeneratedSourceRenderResult(
            completed,
            diagnostics.ToImmutableArray(),
            traceDocuments.ToImmutableArray());
    }
}
