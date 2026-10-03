using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 先頭が Concat で後ろに添字の無い Where/Select だけが並ぶ形を、第1ソース・第2ソースの根をそれぞれ直接回して流すこと。
/// Range の部分で段が偶奇の Where 1つなら2刻みにする。結果と例外は System.Linq と同じ。
/// </summary>
[LinqIntercept(false)]
public sealed class ConcatThenStatelessConsumeTests {
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
            [LinqIntercept(false)]
            private static string Collect(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            public static string RunFLinq(int s,int c,int[] second) => string.Join("|",
                Collect(sink => F.RangeEven(s,c,second,sink)),
                Collect(sink => F.RangeSelectWhere(s,c,second,sink)),
                Collect(sink => F.ArrayWhere(second,sink)),
                Collect(sink => F.LazyWhereSelect(s,c,second,sink)));
            public static string RunLinq(int s,int c,int[] second) => string.Join("|",
                Collect(sink => L.RangeEven(s,c,second,sink)),
                Collect(sink => L.RangeSelectWhere(s,c,second,sink)),
                Collect(sink => L.ArrayWhere(second,sink)),
                Collect(sink => L.LazyWhereSelect(s,c,second,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void RangeEven(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Concat(Yield(second)).Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void RangeSelectWhere(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Concat(second).Select(x => x*3).Where(x => x%2!=0));
                [LinqIntercept(true)] public static void ArrayWhere(int[] second,Sink sink) => sink.Consume(second.Concat(Yield(second).Where(x => x>2)).Where(x => x%3!=0));
                [LinqIntercept(true)] public static void LazyWhereSelect(int s,int c,int[] second,Sink sink) => sink.Consume(Yield(second).Where(x => x!=4).Concat(Enumerable.Range(s,c)).Select(x => x+100));
                [LinqIntercept(false)]
                private static IEnumerable<int> Yield(int[] values) {
                    foreach(var value in values) {
                        yield return value;
                    }
                }
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void RangeEven(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Concat(second).Where(x => (x&1)==0)) sink.Consume(v); }
                public static void RangeSelectWhere(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Concat(second).Select(x => x*3).Where(x => x%2!=0)) sink.Consume(v); }
                public static void ArrayWhere(int[] second,Sink sink) { foreach(var v in second.Concat(second.Where(x => x>2)).Where(x => x%3!=0)) sink.Consume(v); }
                public static void LazyWhereSelect(int s,int c,int[] second,Sink sink) { foreach(var v in second.Where(x => x!=4).Concat(Enumerable.Range(s,c)).Select(x => x+100)) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,10,new[] { 4,12,-3,7,20,15,14,0 })]
    [InlineData(-5,8,new[] { -6,-5,2,3 })]
    [InlineData(1,0,new[] { 1,2,3 })]
    [InlineData(1,5,new int[0])]
    [InlineData(1,-1,new[] { 1 })]
    [InlineData(int.MaxValue-3,4,new[] { int.MaxValue,int.MinValue })]
    public void System_Linqと同じ結果を返す(int start,int count,int[] second) {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"ConcatThenStatelessConsumeProbe",4);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count,second])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count,second])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
        Assert.Contains("__concatSecondRoot",generated);
    }
}
