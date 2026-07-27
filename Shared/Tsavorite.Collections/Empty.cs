using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
namespace Shared.Tsavorite.Collections;


public class TestEqualityEmpty {
    [BenchFact, Benchmark]
    public void opEqual() {
        var a = EqualityEmpty.Default;
        var b = default(EqualityEmpty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.True(a==b);
    }
    [BenchFact, Benchmark]
    public void opInequal() {
        var a = EqualityEmpty.Default;
        var b = default(EqualityEmpty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.False(a!=b);
    }
}
