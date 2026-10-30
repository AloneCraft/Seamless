using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class FloatingPointToleranceTests{
    [Theory,Trait("Category","Unit")]
    [InlineData(0.6,0.6000000000000001,true)]
    [InlineData(0.7999999999999999,0.8,true)]
    [InlineData(1e16,1.0000000000000002e16,true)]
    [InlineData(1.0,1.0001,false)]
    [InlineData(0.0,1e-6,false)]
    [InlineData(0.0,1e-12,true)]
    [InlineData(double.NaN,double.NaN,true)]
    [InlineData(double.NaN,1.0,false)]
    [InlineData(double.PositiveInfinity,double.PositiveInfinity,true)]
    [InlineData(double.PositiveInfinity,double.NegativeInfinity,false)]
    public void 足す順によるずれは許容しそれ以外は許容しない(double expected,double actual,bool match){
        Assert.Equal(match,FloatingPointTolerance.AreNearlyEqual(expected,actual));
    }

    [Fact,Trait("Category","Unit")]
    public void スカラーは許容範囲で一致とする(){
        var left=DynamicQueryValue.Scalar(new DoubleQueryAtom(0.6));
        var right=DynamicQueryValue.Scalar(new DoubleQueryAtom(0.6000000000000001));
        Assert.True(DynamicQueryComparer.Compare(left,right).IsMatch);
        var far=DynamicQueryValue.Scalar(new DoubleQueryAtom(0.61));
        Assert.False(DynamicQueryComparer.Compare(left,far).IsMatch);
    }
}
