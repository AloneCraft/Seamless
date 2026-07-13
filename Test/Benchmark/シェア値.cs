namespace Test.Benchmark;

public sealed class シェア値{
    [Theory]
    [InlineData(0,0,0.0,0.0,"0.00:0.00",global::UI.シェア判定.同等)]
    [InlineData(1,0,1.0,0.0,"1.00:0.00",global::UI.シェア判定.右優位)]
    [InlineData(0,1,0.0,1.0,"0.00:1.00",global::UI.シェア判定.左優位)]
    [InlineData(1,1,0.5,0.5,"0.50:0.50",global::UI.シェア判定.同等)]
    [InlineData(1,9,0.1,0.9,"0.10:0.90",global::UI.シェア判定.左優位)]
    public void メモリから作成_補数のシェアを返す(long left,long right,double expectedLeft,double expectedRight,string display,global::UI.シェア判定 judgement){
        var value=global::UI.シェア値.メモリから作成(left,right);

        Assert.Equal(expectedLeft,value.左シェア!.Value,12);
        Assert.Equal(expectedRight,value.右シェア!.Value,12);
        Assert.Equal(display,value.表示文字列);
        Assert.Equal(judgement,value.判定);
    }

    [Fact]
    public void 作成_負数と非有限値を欠測にする(){
        Assert.True(global::UI.シェア値.メモリから作成(-1,0).欠測か);
        Assert.True(global::UI.シェア値.メモリから作成(0,-1).欠測か);
        Assert.True(global::UI.シェア値.時間から作成(double.NaN,1).欠測か);
        Assert.True(global::UI.シェア値.時間から作成(1,double.PositiveInfinity).欠測か);
    }

    [Fact]
    public void 表示文字列_丸めた合計を一にする(){
        var value=global::UI.シェア値.メモリから作成(995,5);

        Assert.Equal(0.995,value.左シェア!.Value,12);
        Assert.Equal("1.00:0.00",value.表示文字列);
    }

    [Theory]
    [InlineData(94,100,global::UI.シェア判定.左優位)]
    [InlineData(95,100,global::UI.シェア判定.同等)]
    [InlineData(105,100,global::UI.シェア判定.同等)]
    [InlineData(106,100,global::UI.シェア判定.右優位)]
    public void 時間から作成_従来の五パーセント許容を維持する(double left,double right,global::UI.シェア判定 expected){
        Assert.Equal(expected,global::UI.シェア値.時間から作成(left,right).判定);
    }
}
