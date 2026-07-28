namespace Shared.Linq.Optimization;

// 最適化の ON/OFF は DefineConstants で切り替える。
// #elif の排他チェーンにすると 1 個しか無効化できないので、非排他の #if を重ねてビット合成する。
// これで DISABLE_REWRITE と DISABLE_PROJECTION_MERGE の同時指定なども表現できる。
internal static class OptimizationSettings {
    private const global::Common.LinqOptimizations Disabled =
#if DISABLE_REWRITE
        global::Common.LinqOptimizations.Rewrite|
#endif
#if DISABLE_PIPELINE_FUSION
        global::Common.LinqOptimizations.PipelineFusion|
#endif
#if DISABLE_PREDICATE_PUSHDOWN
        global::Common.LinqOptimizations.PredicatePushdown|
#endif
#if DISABLE_PROJECTION_MERGE
        global::Common.LinqOptimizations.ProjectionMerge|
#endif
        global::Common.LinqOptimizations.None;
    public const global::Common.LinqOptimizations Value = global::Common.LinqOptimizations.All&~Disabled;
}
