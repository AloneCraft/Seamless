namespace Seamless.FLinq;

internal static class LinqExpressionAnalyzer {
    internal static FLinqIncrementalGenerator.ローカルメソッドLinq解析結果 Analyze(IMethodSymbol method,Compilation compilation) =>
        FLinqIncrementalGenerator.ローカルメソッドLinq解析取得(method,compilation);
}

internal static class LinqOptimizationExecutor {
    internal static List<PipelineStage> NormalizePipeline(
        List<PipelineStage> stages,
        Func<string,string,string> joinStaticMethods,
        LinqGenerationConfiguration configuration,
        OptimizationTraceCollector? trace) =>
        PipelineBuilder.NormalizeStages(stages,joinStaticMethods,configuration,trace);
}

internal sealed class LinqFragmentBuilder(FLinqIncrementalGenerator.GenerationPipeline pipeline) {
    internal bool TryBuild(
        FLinqIncrementalGenerator.Linq傍受対象型 target,
        out string staticMethods,
        out string body,
        HashSet<string>? emittedUnsafeAccessors) =>
        pipeline.TryBuildLocalMethodBody(target,out staticMethods,out body,emittedUnsafeAccessors);
}

internal static class GeneratedSourceRenderer {
    internal static GeneratedSourceRenderResult Render(GeneratedSourceRenderModel model) =>
        GeneratedSourceRenderCoordinator.Render(model);
}
