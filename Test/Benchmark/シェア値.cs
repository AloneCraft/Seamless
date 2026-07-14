namespace Test.Benchmark;

public sealed class シェア値 {
    [Theory]
    [InlineData(0,0,0.0,0.0,"0.00:0.00",global::UI.シェア判定.同等)]
    [InlineData(1,0,1.0,0.0,"1.00:0.00",global::UI.シェア判定.右優位)]
    [InlineData(0,1,0.0,1.0,"0.00:1.00",global::UI.シェア判定.左優位)]
    [InlineData(1,1,0.5,0.5,"0.50:0.50",global::UI.シェア判定.同等)]
    [InlineData(1,9,0.1,0.9,"0.10:0.90",global::UI.シェア判定.左優位)]
    public void メモリから作成_補数のシェアを返す(long 左,long 右,double 期待左,double 期待右,string 表示,global::UI.シェア判定 判定) {
        var 値 = global::UI.シェア値.メモリから作成(左,右);

        Assert.Equal(期待左,値.左シェア!.Value,12);
        Assert.Equal(期待右,値.右シェア!.Value,12);
        Assert.Equal(表示,値.表示文字列);
        Assert.Equal(判定,値.判定);
    }

    [Fact]
    public void 作成_負数と非有限値を欠測にする() {
        Assert.True(global::UI.シェア値.メモリから作成(-1,0).欠測か);
        Assert.True(global::UI.シェア値.メモリから作成(0,-1).欠測か);
        Assert.True(global::UI.シェア値.時間から作成(double.NaN,1).欠測か);
        Assert.True(global::UI.シェア値.時間から作成(1,double.PositiveInfinity).欠測か);
    }

    [Fact]
    public void 表示文字列_丸めた合計を一にする() {
        var 値 = global::UI.シェア値.メモリから作成(995,5);

        Assert.Equal(0.995,値.左シェア!.Value,12);
        Assert.Equal("1.00:0.00",値.表示文字列);
    }

    [Theory]
    [InlineData(94,100,global::UI.シェア判定.左優位)]
    [InlineData(95,100,global::UI.シェア判定.同等)]
    [InlineData(105,100,global::UI.シェア判定.同等)]
    [InlineData(106,100,global::UI.シェア判定.右優位)]
    public void 時間から作成_従来の五パーセント許容を維持する(double 左,double 右,global::UI.シェア判定 期待値) {
        Assert.Equal(期待値,global::UI.シェア値.時間から作成(左,右).判定);
    }

    [Theory]
    [InlineData(80,20,0.80,0.20,true)]
    [InlineData(61,39,0.61,0.39,true)]
    [InlineData(69,31,0.69,0.31,true)]
    [InlineData(100,0,1.00,0.00,true)]
    [InlineData(0,0,0.00,0.00,false)]
    public void ベンチマークシェアゲージ状態は丸め前の左右割合と色表示状態を返す(
        long 左,
        long 右,
        double 期待左,
        double 期待右,
        bool 色付き期待値か) {
        var ゲージ = global::UI.ベンチマークシェアゲージ状態.シェアから作成(global::UI.シェア値.メモリから作成(左,右));

        Assert.Equal(期待左,ゲージ.左割合,12);
        Assert.Equal(期待右,ゲージ.右割合,12);
        Assert.Equal(色付き期待値か,ゲージ.色付きバーがあるか);
    }

    [Fact]
    public void ベンチマークシェアゲージ状態は欠測時に色を表示しない() {
        var ゲージ = global::UI.ベンチマークシェアゲージ状態.シェアから作成(global::UI.シェア値.欠測);

        Assert.False(ゲージ.色付きバーがあるか);
        Assert.Equal(0,ゲージ.左割合);
        Assert.Equal(0,ゲージ.右割合);
    }
}
