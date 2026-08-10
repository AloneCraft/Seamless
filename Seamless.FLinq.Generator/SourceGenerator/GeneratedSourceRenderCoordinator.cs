namespace Seamless.FLinq;

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
            out var traceDocuments);
        return new GeneratedSourceRenderResult(
            source,
            diagnostics.ToImmutableArray(),
            traceDocuments.ToImmutableArray());
    }
}
