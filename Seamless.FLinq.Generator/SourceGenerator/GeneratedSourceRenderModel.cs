namespace Seamless.FLinq;

/// <summary>
/// CoordinatorがRendererへ渡す、1ファイル分の生成対象の不変スナップショットです。
/// </summary>
internal sealed class GeneratedSourceRenderModel(
    ImmutableArray<FLinqIncrementalGenerator.Linq傍受対象型> targets) {
    internal ImmutableArray<FLinqIncrementalGenerator.Linq傍受対象型> Targets { get; } = targets;
}

/// <summary>
/// RendererがCoordinatorへ返す生成ソース、診断、最適化traceの不変結果です。
/// </summary>
internal sealed class GeneratedSourceRenderResult(
    string source,
    ImmutableArray<Diagnostic> diagnostics,
    ImmutableArray<OptimizationTraceDocument> traceDocuments) {
    internal string Source { get; } = source;
    internal ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
    internal ImmutableArray<OptimizationTraceDocument> TraceDocuments { get; } = traceDocuments;
}
