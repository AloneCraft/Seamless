namespace Shared.Linq.Optimization;

internal static class OptimizationSettings {
    public const global::Common.LinqOptimizations Value =
#if DISABLE_REWRITE
        global::Common.LinqOptimizations.All&~global::Common.LinqOptimizations.Rewrite;
#elif DISABLE_PIPELINE_FUSION
        global::Common.LinqOptimizations.All&~global::Common.LinqOptimizations.PipelineFusion;
#elif DISABLE_PREDICATE_PUSHDOWN
        global::Common.LinqOptimizations.All&~global::Common.LinqOptimizations.PredicatePushdown;
#else
        global::Common.LinqOptimizations.All;
#endif
}
