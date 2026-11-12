namespace Benchmark.Dynamic.Tests;

internal sealed partial class StaticBuildFixture{
    internal static int RunChain(int[] values)=>StaticChain(values);
    internal static int RunTyped(Row[] values)=>StaticTyped(values);
}
