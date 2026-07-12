namespace Test.Benchmark;

public sealed class AllocationShareValue{
    [Theory]
    [InlineData(0,0,0.5,0.5,"0.50:0.50",global::UI.RatioJudgement.Tie)]
    [InlineData(1,0,1.0,0.0,"1.00:0.00",global::UI.RatioJudgement.LeftLose)]
    [InlineData(0,1,0.0,1.0,"0.00:1.00",global::UI.RatioJudgement.LeftWin)]
    [InlineData(1,1,0.5,0.5,"0.50:0.50",global::UI.RatioJudgement.Tie)]
    [InlineData(1,9,0.1,0.9,"0.10:0.90",global::UI.RatioJudgement.LeftWin)]
    public void FromBytes_CreatesComplementaryShares(
        long left,
        long right,
        double expectedLeft,
        double expectedRight,
        string display,
        global::UI.RatioJudgement judgement){
        var value = global::UI.AllocationShareValue.FromBytes(left,right);

        Assert.Equal(expectedLeft,value.LeftShare!.Value,12);
        Assert.Equal(expectedRight,value.RightShare!.Value,12);
        Assert.Equal(1,value.LeftShare.Value+value.RightShare.Value,12);
        Assert.Equal(display,value.DisplayText);
        Assert.Equal(judgement,value.Judgement);
    }

    [Fact]
    public void FromBytes_RejectsNegativeMeasurements(){
        Assert.True(global::UI.AllocationShareValue.FromBytes(-1,0).IsMissing);
        Assert.True(global::UI.AllocationShareValue.FromBytes(0,-1).IsMissing);
    }

    [Fact]
    public void DisplayText_RoundsAsComplements(){
        var value = global::UI.AllocationShareValue.FromBytes(995,5);

        Assert.Equal("1.00:0.00",value.DisplayText);
    }
}
