using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range の集計(Sum/Average/Min/Max/Aggregate)を和の公式と単調性で求める変換が、System.Linq と同じ値と例外を返すこと。
/// 境界: 空、int の Sum の桁あふれ(最後の和・負の要素の途中の和)、1次式の選択子の桁あふれ(数え上げに戻る)、
/// double の逐次加算が丸める大きさ(数え上げに戻る)、1次式でない選択子(従来の経路)。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeArithmeticClosedFormTests {
    private const string Source = """
        using System;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Probe {
            private static string Describe<T>(Func<T> evaluate) {
                try {
                    return Convert.ToString(evaluate(),System.Globalization.CultureInfo.InvariantCulture)!;
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
            }
            public static string RunFLinq(int s,int c) => string.Join(";",
                Describe(() => F.Sum(s,c)),
                Describe(() => F.SumInt(s,c)),
                Describe(() => F.SumLong(s,c)),
                Describe(() => F.SumDouble(s,c)),
                Describe(() => F.Average(s,c)),
                Describe(() => F.AverageLong(s,c)),
                Describe(() => F.AverageDouble(s,c)),
                Describe(() => F.MaxTwice(s,c)),
                Describe(() => F.MinTwice(s,c)),
                Describe(() => F.MaxDecreasing(s,c)),
                Describe(() => F.MinDecreasing(s,c)),
                Describe(() => F.MaxLong(s,c)),
                Describe(() => F.MinDouble(s,c)),
                Describe(() => F.MaxSquare(s,c)),
                Describe(() => F.AggregateLong(s,c)),
                Describe(() => F.AggregateInt(s,c)),
                Describe(() => F.AggregateSeedless(s,c)),
                Describe(() => F.AggregateResult(s,c)));
            public static string RunLinq(int s,int c) => string.Join(";",
                Describe(() => L.Sum(s,c)),
                Describe(() => L.SumInt(s,c)),
                Describe(() => L.SumLong(s,c)),
                Describe(() => L.SumDouble(s,c)),
                Describe(() => L.Average(s,c)),
                Describe(() => L.AverageLong(s,c)),
                Describe(() => L.AverageDouble(s,c)),
                Describe(() => L.MaxTwice(s,c)),
                Describe(() => L.MinTwice(s,c)),
                Describe(() => L.MaxDecreasing(s,c)),
                Describe(() => L.MinDecreasing(s,c)),
                Describe(() => L.MaxLong(s,c)),
                Describe(() => L.MinDouble(s,c)),
                Describe(() => L.MaxSquare(s,c)),
                Describe(() => L.AggregateLong(s,c)),
                Describe(() => L.AggregateInt(s,c)),
                Describe(() => L.AggregateSeedless(s,c)),
                Describe(() => L.AggregateResult(s,c)));
        }
        public sealed class F {
            [LinqIntercept(true)] public static int Sum(int s,int c) => Enumerable.Range(s,c).Sum();
            [LinqIntercept(true)] public static int SumInt(int s,int c) => Enumerable.Range(s,c).Sum(x => x);
            [LinqIntercept(true)] public static long SumLong(int s,int c) => Enumerable.Range(s,c).Sum(x => (long)x);
            [LinqIntercept(true)] public static double SumDouble(int s,int c) => Enumerable.Range(s,c).Sum(x => (double)x);
            [LinqIntercept(true)] public static double Average(int s,int c) => Enumerable.Range(s,c).Average();
            [LinqIntercept(true)] public static double AverageLong(int s,int c) => Enumerable.Range(s,c).Average(x => (long)x);
            [LinqIntercept(true)] public static double AverageDouble(int s,int c) => Enumerable.Range(s,c).Average(x => (double)x);
            [LinqIntercept(true)] public static int MaxTwice(int s,int c) => Enumerable.Range(s,c).Max(x => x*2);
            [LinqIntercept(true)] public static int MinTwice(int s,int c) => Enumerable.Range(s,c).Min(x => x*2);
            [LinqIntercept(true)] public static int MaxDecreasing(int s,int c) => Enumerable.Range(s,c).Max(x => 5-x*3);
            [LinqIntercept(true)] public static int MinDecreasing(int s,int c) => Enumerable.Range(s,c).Min(x => -(x+1)*3);
            [LinqIntercept(true)] public static long MaxLong(int s,int c) => Enumerable.Range(s,c).Max(x => (long)x);
            [LinqIntercept(true)] public static double MinDouble(int s,int c) => Enumerable.Range(s,c).Min(x => (double)x);
            [LinqIntercept(true)] public static int MaxSquare(int s,int c) => Enumerable.Range(s,c).Max(x => x*x);
            [LinqIntercept(true)] public static long AggregateLong(int s,int c) => Enumerable.Range(s,c).Aggregate(0L,(a,x) => a+x);
            [LinqIntercept(true)] public static int AggregateInt(int s,int c) => Enumerable.Range(s,c).Aggregate(10,(a,x) => x+a);
            [LinqIntercept(true)] public static int AggregateSeedless(int s,int c) => Enumerable.Range(s,c).Aggregate((a,x) => a+x);
            [LinqIntercept(true)] public static double AggregateResult(int s,int c) => Enumerable.Range(s,c).Aggregate(7L,(a,x) => a+x,a => a/2.0);
        }
        [LinqIntercept(false)]
        public sealed class L {
            public static int Sum(int s,int c) => Enumerable.Range(s,c).Sum();
            public static int SumInt(int s,int c) => Enumerable.Range(s,c).Sum(x => x);
            public static long SumLong(int s,int c) => Enumerable.Range(s,c).Sum(x => (long)x);
            public static double SumDouble(int s,int c) => Enumerable.Range(s,c).Sum(x => (double)x);
            public static double Average(int s,int c) => Enumerable.Range(s,c).Average();
            public static double AverageLong(int s,int c) => Enumerable.Range(s,c).Average(x => (long)x);
            public static double AverageDouble(int s,int c) => Enumerable.Range(s,c).Average(x => (double)x);
            public static int MaxTwice(int s,int c) => Enumerable.Range(s,c).Max(x => x*2);
            public static int MinTwice(int s,int c) => Enumerable.Range(s,c).Min(x => x*2);
            public static int MaxDecreasing(int s,int c) => Enumerable.Range(s,c).Max(x => 5-x*3);
            public static int MinDecreasing(int s,int c) => Enumerable.Range(s,c).Min(x => -(x+1)*3);
            public static long MaxLong(int s,int c) => Enumerable.Range(s,c).Max(x => (long)x);
            public static double MinDouble(int s,int c) => Enumerable.Range(s,c).Min(x => (double)x);
            public static int MaxSquare(int s,int c) => Enumerable.Range(s,c).Max(x => x*x);
            public static long AggregateLong(int s,int c) => Enumerable.Range(s,c).Aggregate(0L,(a,x) => a+x);
            public static int AggregateInt(int s,int c) => Enumerable.Range(s,c).Aggregate(10,(a,x) => x+a);
            public static int AggregateSeedless(int s,int c) => Enumerable.Range(s,c).Aggregate((a,x) => a+x);
            public static double AggregateResult(int s,int c) => Enumerable.Range(s,c).Aggregate(7L,(a,x) => a+x,a => a/2.0);
        }
        """;

    [Theory]
    [InlineData(1,0)]
    [InlineData(1,1)]
    [InlineData(1,4096)]
    [InlineData(-5,11)]
    [InlineData(-3,3)]
    // int の Sum: 最後の和がちょうど収まる/あふれる。
    [InlineData(0,65536)]
    [InlineData(0,65537)]
    // 最後の和は 0 だが、負の要素の途中の和があふれる(System.Linq は OverflowException)。
    [InlineData(-70000,140001)]
    [InlineData(int.MinValue,3)]
    // x*2 が int を外れる(1次式の端点が収まらず数え上げに戻る)。
    [InlineData(int.MaxValue-10,11)]
    [InlineData(1_073_741_820,8)]
    // double の逐次加算が丸める大きさ(数え上げに戻る)。
    [InlineData(int.MinValue,5_000_000)]
    public void System_Linqと同じ値と例外を返す(int start,int count) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"RangeArithmeticClosedFormProbe",18);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected,actual);
    }

    [Fact]
    public void 一次式の選択子と和の畳み込みだけを数式にする() {
        var (_,generated) = ExecutableGeneration.Generate(Source,"RangeArithmeticClosedFormShape",18);
        // 和の公式: Sum 4つ・Average 3つ・Aggregate 4つ。端の値: x*2・5-x*3・-(x+1)*3・(long)x・(double)x の6つ(x*x は従来の経路)。
        Assert.Equal(11,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var __sum\d+=\(long\)").Count);
        Assert.Equal(6,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var __first\d+=").Count);
    }
}
