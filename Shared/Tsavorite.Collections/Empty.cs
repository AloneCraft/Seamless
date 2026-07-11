using EqualityEmpty = Seamless.Tsavorite.Collections.EqualityEmpty;
#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif


public class TestEqualityEmpty {
    [BenchFact,Benchmark]
    public void opEqual() {
        var a = EqualityEmpty.Default;
        var b = default(EqualityEmpty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.True(a==b);
    }
    [BenchFact,Benchmark]
    public void opInequal() {
        var a = EqualityEmpty.Default;
        var b = default(EqualityEmpty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.False(a!=b);
    }
}
