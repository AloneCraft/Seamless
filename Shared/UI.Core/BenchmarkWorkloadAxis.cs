namespace UI;

internal static class BenchmarkWorkloadAxis{
    public const int StandardElementCount = 4096;
    public const int DefaultLoopCount = 1;

    public static bool UsesLoopCount(BenchmarkSourceTab sourceTab) =>
        !IsScalableSourceTab(sourceTab);

    public static bool IsScalableSourceTab(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalableSet
            or BenchmarkSourceTab.ScalableDictionary
            or BenchmarkSourceTab.ScalablePersistedList
            or BenchmarkSourceTab.ScalablePersistedSet
            or BenchmarkSourceTab.ScalableTsavoriteSet
            or BenchmarkSourceTab.ScalablePersistedDictionary
            or BenchmarkSourceTab.ScalableTsavoriteDictionary;

    public static int ResolveElementCount(BenchmarkSourceTab sourceTab,int axisCount) =>
        axisCount<=0 ? 0 : UsesLoopCount(sourceTab) ? StandardElementCount : axisCount;

    public static int ResolveLoopCount(BenchmarkSourceTab sourceTab,int axisCount) =>
        axisCount<=0 ? 0 : UsesLoopCount(sourceTab) ? axisCount : DefaultLoopCount;

    public static IReadOnlyList<int> ResolvePreparedElementCounts(BenchmarkSourceTab sourceTab,IEnumerable<int> axisCounts) =>
        UsesLoopCount(sourceTab)
            ? [StandardElementCount]
            : axisCounts.Where(static count=>count>0).Distinct().Order().ToArray();

    public static int GetAxisCount(BenchmarkJob job) =>
        UsesLoopCount(job.SourceTab)&&job.LoopCount>0 ? job.LoopCount : job.ElementCount;

    public static int GetAxisCount(BenchmarkResultItem result) =>
        UsesLoopCount(result.SourceTab)&&result.LoopCount>0 ? result.LoopCount : result.ElementCount;

    public static double? NormalizePerOperation(BenchmarkSourceTab sourceTab,int loopCount,double? total) =>
        total is { } value&&UsesLoopCount(sourceTab)&&loopCount>1 ? value/loopCount : total;

    public static long? NormalizePerOperation(BenchmarkSourceTab sourceTab,int loopCount,long? total) =>
        total is { } value&&UsesLoopCount(sourceTab)&&loopCount>1 ? value/loopCount : total;
}
