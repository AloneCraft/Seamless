using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range のキー x%C の DistinctBy・MaxBy・MinBy・CountBy を、キーが周期 C で繰り返すことから先頭 C 個だけで求める変換が
/// System.Linq と同じ結果・例外を返すこと。負の値(C# の剰余は負)・0 をまたぐ区間・int の端・空を含める。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeModuloKeyTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Sink {
            public readonly List<string> Values = new();
            public void Consume<T>(T value) => this.Values.Add(value!.ToString()!);
        }
        public sealed class Probe {
            private static string Collect(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            private static string Describe<T>(Func<T> evaluate) {
                try {
                    var value = evaluate();
                    return value is IEnumerable<int> values ? string.Join(",",values) : Convert.ToString(value)!;
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
            }
            public static string RunFLinq(int s,int c) => string.Join("|",
                Collect(sink => F.Distinct7(s,c,sink)),
                Collect(sink => F.Distinct1000(s,c,sink)),
                Collect(sink => F.Count7(s,c,sink)),
                Collect(sink => F.Count1000(s,c,sink)),
                Describe(() => F.DistinctList(s,c)),
                Describe(() => F.Max7(s,c)),
                Describe(() => F.Min7(s,c)),
                Describe(() => F.Max1000(s,c)),
                Describe(() => F.Min1000(s,c)));
            public static string RunLinq(int s,int c) => string.Join("|",
                Collect(sink => L.Distinct7(s,c,sink)),
                Collect(sink => L.Distinct1000(s,c,sink)),
                Collect(sink => L.Count7(s,c,sink)),
                Collect(sink => L.Count1000(s,c,sink)),
                Describe(() => L.DistinctList(s,c)),
                Describe(() => L.Max7(s,c)),
                Describe(() => L.Min7(s,c)),
                Describe(() => L.Max1000(s,c)),
                Describe(() => L.Min1000(s,c)));
            public sealed class F {
                [LinqIntercept(true)] public static void Distinct7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).DistinctBy(x => x%7));
                [LinqIntercept(true)] public static void Distinct1000(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).DistinctBy(x => x%1000));
                [LinqIntercept(true)] public static List<int> DistinctList(int s,int c) => Enumerable.Range(s,c).DistinctBy(x => x%7).ToList();
                [LinqIntercept(true)] public static void Count7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).CountBy(x => x%7));
                [LinqIntercept(true)] public static void Count1000(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).CountBy(x => x%1000));
                [LinqIntercept(true)] public static int Max7(int s,int c) => Enumerable.Range(s,c).MaxBy(x => x%7);
                [LinqIntercept(true)] public static int Min7(int s,int c) => Enumerable.Range(s,c).MinBy(x => x%7);
                [LinqIntercept(true)] public static int Max1000(int s,int c) => Enumerable.Range(s,c).MaxBy(x => x%1000);
                [LinqIntercept(true)] public static int Min1000(int s,int c) => Enumerable.Range(s,c).MinBy(x => x%1000);
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void Distinct7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).DistinctBy(x => x%7)) sink.Consume(v); }
                public static void Distinct1000(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).DistinctBy(x => x%1000)) sink.Consume(v); }
                public static List<int> DistinctList(int s,int c) => Enumerable.Range(s,c).DistinctBy(x => x%7).ToList();
                public static void Count7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).CountBy(x => x%7)) sink.Consume(v); }
                public static void Count1000(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).CountBy(x => x%1000)) sink.Consume(v); }
                public static int Max7(int s,int c) => Enumerable.Range(s,c).MaxBy(x => x%7);
                public static int Min7(int s,int c) => Enumerable.Range(s,c).MinBy(x => x%7);
                public static int Max1000(int s,int c) => Enumerable.Range(s,c).MaxBy(x => x%1000);
                public static int Min1000(int s,int c) => Enumerable.Range(s,c).MinBy(x => x%1000);
            }
        }
        """;

    [Theory]
    [InlineData(1,0)]
    [InlineData(0,1)]
    [InlineData(1,5)]
    [InlineData(5,4)]
    [InlineData(3,6)]
    [InlineData(1,4096)]
    [InlineData(998,5)]
    [InlineData(-20,45)]
    [InlineData(-5,3)]
    [InlineData(-14,15)]
    [InlineData(-7,8)]
    [InlineData(-3,9)]
    [InlineData(int.MaxValue-10,11)]
    [InlineData(int.MinValue,10)]
    [InlineData(1,-1)]
    public void System_Linqと同じ結果を返す(int start,int count) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"RangeModuloKeyProbe",9);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }

    [Fact]
    public void 剰余のキーを周期から求める() {
        var (_,generated) = ExecutableGeneration.Generate(Source,"RangeModuloKeyShape",9);
        // DistinctBy は Consume 2つと ToList 1つ、MaxBy/MinBy は4つ。
        Assert.Equal(3,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var skipZero\d*=").Count);
        Assert.Equal(4,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var bestKey\d*=").Count);
        // CountBy は Consume 2つ。
        Assert.Equal(2,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var counts\d*=").Count);
    }
}
