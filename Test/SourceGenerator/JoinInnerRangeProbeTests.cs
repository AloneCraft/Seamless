using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 内側が Range で内側のキーが恒等(int)の Join は、探索表を作らず外側のキーが区間に入るかで一致を判定する。
/// 結果(外側の順・一致ごとに1件)が System.Linq と同じこと。重なり・離れ・空・負の値・外側の重複・外側のキーが恒等でない形を含める。
/// </summary>
[LinqIntercept(false)]
public sealed class JoinInnerRangeProbeTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Sink {
            public readonly List<int> Values = new();
            public void Consume<T>(T value) => this.Values.Add((int)(object)value!);
        }
        public sealed class Probe {
            private static readonly int[] Outer = [5,3,5,-2,8,0,3];
            private static string Collect(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            private static string Describe(Func<List<int>> evaluate) {
                try {
                    return string.Join(",",evaluate());
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
            }
            public static string RunFLinq(int s,int c,int s2,int c2) => string.Join("|",
                Collect(sink => F.RangeRange(s,c,s2,c2,sink)),
                Collect(sink => F.Doubled(s,c,s2,c2,sink)),
                Collect(sink => F.ArrayOuter(s2,c2,sink)),
                Describe(() => F.RangeRangeList(s,c,s2,c2)),
                Describe(() => F.InnerNotIdentity(s,c,s2,c2)));
            public static string RunLinq(int s,int c,int s2,int c2) => string.Join("|",
                Collect(sink => L.RangeRange(s,c,s2,c2,sink)),
                Collect(sink => L.Doubled(s,c,s2,c2,sink)),
                Collect(sink => L.ArrayOuter(s2,c2,sink)),
                Describe(() => L.RangeRangeList(s,c,s2,c2)),
                Describe(() => L.InnerNotIdentity(s,c,s2,c2)));
            public sealed class F {
                [LinqIntercept(true)] public static void RangeRange(int s,int c,int s2,int c2,Sink sink) => sink.Consume(Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x,x => x,(a,b) => a+b));
                [LinqIntercept(true)] public static void Doubled(int s,int c,int s2,int c2,Sink sink) => sink.Consume(Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x*2,x => x,(a,b) => a*1000+b));
                [LinqIntercept(true)] public static void ArrayOuter(int s2,int c2,Sink sink) => sink.Consume(Outer.Join(Enumerable.Range(s2,c2),x => x,x => x,(a,b) => a*10+b));
                [LinqIntercept(true)] public static List<int> RangeRangeList(int s,int c,int s2,int c2) => Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x,x => x,(a,b) => a-b).ToList();
                [LinqIntercept(true)] public static List<int> InnerNotIdentity(int s,int c,int s2,int c2) => Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x%3,x => x%3,(a,b) => a*100+b).ToList();
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void RangeRange(int s,int c,int s2,int c2,Sink sink) { foreach(var v in Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x,x => x,(a,b) => a+b)) sink.Consume(v); }
                public static void Doubled(int s,int c,int s2,int c2,Sink sink) { foreach(var v in Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x*2,x => x,(a,b) => a*1000+b)) sink.Consume(v); }
                public static void ArrayOuter(int s2,int c2,Sink sink) { foreach(var v in Outer.Join(Enumerable.Range(s2,c2),x => x,x => x,(a,b) => a*10+b)) sink.Consume(v); }
                public static List<int> RangeRangeList(int s,int c,int s2,int c2) => Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x,x => x,(a,b) => a-b).ToList();
                public static List<int> InnerNotIdentity(int s,int c,int s2,int c2) => Enumerable.Range(s,c).Join(Enumerable.Range(s2,c2),x => x%3,x => x%3,(a,b) => a*100+b).ToList();
            }
        }
        """;

    [Theory]
    [InlineData(1,10,6,5)]
    [InlineData(1,10,20,5)]
    [InlineData(1,10,-3,6)]
    [InlineData(1,0,1,5)]
    [InlineData(1,5,1,0)]
    [InlineData(-5,10,-2,3)]
    [InlineData(1,4096,2049,2048)]
    [InlineData(int.MaxValue-3,4,int.MaxValue-1,2)]
    [InlineData(1,-1,1,5)]
    [InlineData(1,5,1,-1)]
    [InlineData(int.MaxValue,2,1,5)]
    [InlineData(10,5,1,3)]
    public void System_Linqと同じ結果を返す(int start,int count,int innerStart,int innerCount) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"JoinInnerRangeProbe",5);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count,innerStart,innerCount])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count,innerStart,innerCount])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }

    [Fact]
    public void 内側が恒等キーのRangeのときだけ探索表を作らない() {
        var (_,generated) = ExecutableGeneration.Generate(Source,"JoinInnerRangeProbeShape",5);
        // 区間判定は4つ(x%3 のキーは従来の探索表)。
        Assert.Equal(4,global::System.Text.RegularExpressions.Regex.Matches(generated,@"private long innerLow\d*;").Count);
        Assert.Contains("BuildLookup(",generated);
        // 外側も恒等キーの Range なら、Consume は2つの区間の重なりだけを回す。
        Assert.Contains("joinOverlapLow",generated);
    }
}
