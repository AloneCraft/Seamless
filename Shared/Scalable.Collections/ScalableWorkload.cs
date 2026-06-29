namespace Scalable.Collections;

public static class ScalableElementCounts {
    private const int BaseElementCountExponent = 16;
    private const int ElementCountExponentCount = 8;
    public const int BaseElementCount = 1<<BaseElementCountExponent;

    public static IReadOnlyList<int> All { get; } =
        Enumerable.Range(BaseElementCountExponent,ElementCountExponentCount).Select(static exponent => 1<<exponent).ToArray();

    public static IReadOnlyList<int> DefaultInitial { get; } =
        [BaseElementCount];
}

public static class ScalableWorkload {
    public const int TestElementCount = 1024;

    public static int ElementCount =>
        Common.BenchmarkExecutionMode.IsTestMode
            ? TestElementCount
            : Common.BenchmarkExecutionMode.ScaleElementCount(ScalableElementCounts.BaseElementCount);

    public static int ReadIndex(int count) =>
        count<=1 ? 0 : count/2;
}
