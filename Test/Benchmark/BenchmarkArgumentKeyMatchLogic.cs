using UI;

namespace Test.Benchmark;

public sealed class BenchmarkArgumentKeyMatchLogic{
    [Fact]
    public void IsExactMatch_DoesNotTreatEmptyRightAsWildcard(){
        Assert.True(global::UI.BenchmarkArgumentKeyMatchLogic.IsExactMatch("Count=1","Count=1"));
        Assert.True(global::UI.BenchmarkArgumentKeyMatchLogic.IsExactMatch(string.Empty,string.Empty));
        Assert.False(global::UI.BenchmarkArgumentKeyMatchLogic.IsExactMatch("Count=1",string.Empty));
        Assert.False(global::UI.BenchmarkArgumentKeyMatchLogic.IsExactMatch(string.Empty,"Count=1"));
        Assert.False(global::UI.BenchmarkArgumentKeyMatchLogic.IsExactMatch("Count=1","Count=2"));
    }
}



