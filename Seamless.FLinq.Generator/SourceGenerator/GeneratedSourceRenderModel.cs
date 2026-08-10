namespace Seamless.FLinq;

/// <summary>
/// CoordinatorがRendererへ渡す、1ファイル分の生成対象と診断の不変スナップショットです。
/// </summary>
internal sealed class GeneratedSourceRenderModel(
    ImmutableArray<FLinqIncrementalGenerator.Linq傍受対象型> targets,
    List<Diagnostic> diagnostics) {
    internal ImmutableArray<FLinqIncrementalGenerator.Linq傍受対象型> Targets { get; } = targets;
    internal List<Diagnostic> Diagnostics { get; } = diagnostics;
}