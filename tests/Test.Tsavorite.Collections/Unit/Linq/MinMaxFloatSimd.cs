using Tsavorite.Carrier;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Unit.Linq;

// float/double の Min/Max は SIMD 化されている。NaN/±Inf/±0/長さ境界を含めて
// System.Linq.Min/Max と一致することを検証する。
public sealed class MinMaxFloatSimd {
    [Fact, Trait("Category","Unit")]
    public void Double_Min_Max_RandomIncludingSpecials() {
        var rng = new Random(12345);
        // SIMD のベクトル幅・端数・overlap を網羅するため複数長で試す。
        foreach(var len in new[]{ 1,2,3,4,7,8,15,16,31,32,33,63,64,100,1000 }) {
            for(var trial = 0;trial<20;trial++) {
                var data = new double[len];
                for(var i = 0;i<len;i++) {
                    data[i]=NextSpecialOrRandomDouble(rng);
                }
                AssertSame(data);
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Double_Min_Max_AllNaN() {
        var data = new double[]{ double.NaN,double.NaN,double.NaN,double.NaN,double.NaN };
        AssertSame(data);
    }

    [Fact, Trait("Category","Unit")]
    public void Double_Min_Max_NaNMixed() {
        AssertSame([1.0,double.NaN,3.0]);
        AssertSame([double.NaN,1.0,3.0]);
        AssertSame([1.0,3.0,double.NaN]);
        AssertSame([double.NegativeInfinity,double.NaN,double.PositiveInfinity]);
    }

    [Fact, Trait("Category","Unit")]
    public void Float_Min_Max_RandomIncludingSpecials() {
        var rng = new Random(67890);
        foreach(var len in new[]{ 1,2,3,7,8,15,16,31,32,33,63,64,100,1000 }) {
            for(var trial = 0;trial<20;trial++) {
                var data = new float[len];
                for(var i = 0;i<len;i++) {
                    data[i]=(float)NextSpecialOrRandomDouble(rng);
                }
                AssertSameFloat(data);
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Float_Min_Max_NaNMixed() {
        AssertSameFloat([1f,float.NaN,3f]);
        AssertSameFloat([float.NaN,1f,3f]);
        AssertSameFloat([float.NegativeInfinity,float.NaN,float.PositiveInfinity]);
    }

    static double NextSpecialOrRandomDouble(Random rng) {
        return rng.Next(0,10) switch {
            0 => double.NaN,
            1 => double.PositiveInfinity,
            2 => double.NegativeInfinity,
            3 => 0.0,
            4 => -0.0,
            _ => (rng.NextDouble()-0.5)*1_000_000,
        };
    }

    static void AssertSame(double[] data) {
        var expectedMin = SystemEnumerable.Min(data);
        var actualMin = data.AsValueEnumerable().Min();
        Assert.True(EqualDouble(expectedMin,actualMin),$"Min mismatch: expected={expectedMin}, actual={actualMin}, data=[{string.Join(",",data)}]");

        var expectedMax = SystemEnumerable.Max(data);
        var actualMax = data.AsValueEnumerable().Max();
        Assert.True(EqualDouble(expectedMax,actualMax),$"Max mismatch: expected={expectedMax}, actual={actualMax}, data=[{string.Join(",",data)}]");
    }

    static void AssertSameFloat(float[] data) {
        var expectedMin = SystemEnumerable.Min(data);
        var actualMin = data.AsValueEnumerable().Min();
        Assert.True(EqualFloat(expectedMin,actualMin),$"Min mismatch: expected={expectedMin}, actual={actualMin}, data=[{string.Join(",",data)}]");

        var expectedMax = SystemEnumerable.Max(data);
        var actualMax = data.AsValueEnumerable().Max();
        Assert.True(EqualFloat(expectedMax,actualMax),$"Max mismatch: expected={expectedMax}, actual={actualMax}, data=[{string.Join(",",data)}]");
    }

    // NaN==NaN を真として扱う。±0 は IEEE 等価（符号ビットは問わない）。
    static bool EqualDouble(double a,double b) => (double.IsNaN(a) && double.IsNaN(b)) || a==b;
    static bool EqualFloat(float a,float b) => (float.IsNaN(a) && float.IsNaN(b)) || a==b;
}
