namespace UI;

public sealed class BenchmarkArgumentKeyMatchLogicTests{
    [Fact]
    public void IsExactMatch_DoesNotTreatEmptyRightAsWildcard(){
        Assert.True(BenchmarkArgumentKeyMatchLogic.IsExactMatch("Count=1","Count=1"));
        Assert.True(BenchmarkArgumentKeyMatchLogic.IsExactMatch(string.Empty,string.Empty));
        Assert.False(BenchmarkArgumentKeyMatchLogic.IsExactMatch("Count=1",string.Empty));
        Assert.False(BenchmarkArgumentKeyMatchLogic.IsExactMatch(string.Empty,"Count=1"));
        Assert.False(BenchmarkArgumentKeyMatchLogic.IsExactMatch("Count=1","Count=2"));
    }
}
