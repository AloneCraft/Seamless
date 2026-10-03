using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 昇順の Range の後に添字の無い Where が並び、最後が Union のとき、Range の部分は集合を使わずに流し、
/// 第2ソースの値は「区間に入り同じ述語を通るか」で Range の部分との重複を判定すること。結果は System.Linq と同じ。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeUnionConsumeTests {
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
                Collect(sink => F.Even(s,c,second,sink)),
                Collect(sink => F.Odd(s,c,second,sink)),
                Collect(sink => F.Third(s,c,second,sink)),
                Collect(sink => F.Two(s,c,second,sink)),
                Collect(sink => F.Plain(s,c,second,sink)),
                Collect(sink => F.Lazy(s,c,second,sink)));
            public static string RunLinq(int s,int c,int[] second) => string.Join("|",
                Collect(sink => L.Even(s,c,second,sink)),
                Collect(sink => L.Odd(s,c,second,sink)),
                Collect(sink => L.Third(s,c,second,sink)),
                Collect(sink => L.Two(s,c,second,sink)),
                Collect(sink => L.Plain(s,c,second,sink)),
                Collect(sink => L.Lazy(s,c,second,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void Even(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Union(second).Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void Odd(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Union(second).Where(x => (x&1)==1));
                [LinqIntercept(true)] public static void Third(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Union(second).Where(x => x%3==0));
                [LinqIntercept(true)] public static void Two(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where(x => x>2).Where(x => x%2==0).Union(second));
                [LinqIntercept(true)] public static void Plain(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Union(second));
                [LinqIntercept(true)] public static void Lazy(int s,int c,int[] second,Sink sink) => sink.Consume(Enumerable.Range(s,c).Union(Yield(second)).Where(x => (x&1)==0));
                [LinqIntercept(false)]
                private static IEnumerable<int> Yield(int[] values) {
                    foreach(var value in values) {
                        yield return value;
                    }
                }
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void Even(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Union(second).Where(x => (x&1)==0)) sink.Consume(v); }
                public static void Odd(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Union(second).Where(x => (x&1)==1)) sink.Consume(v); }
                public static void Third(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Union(second).Where(x => x%3==0)) sink.Consume(v); }
                public static void Two(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where(x => x>2).Where(x => x%2==0).Union(second)) sink.Consume(v); }
                public static void Plain(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Union(second)) sink.Consume(v); }
                public static void Lazy(int s,int c,int[] second,Sink sink) { foreach(var v in Enumerable.Range(s,c).Union(second).Where(x => (x&1)==0)) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,10,new[] { 4,12,12,-3,7,20,15,14,0 })]
    [InlineData(-5,8,new[] { -6,-5,2,3,8,-6,-4 })]
    [InlineData(1,0,new[] { 1,2,2,3 })]
    [InlineData(1,5,new int[0])]
    [InlineData(1,-1,new[] { 1 })]
    [InlineData(int.MaxValue-3,4,new[] { int.MaxValue,int.MinValue,0,int.MaxValue-4 })]
    public void System_Linqと同じ結果を返す(int start,int count,int[] second) {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"RangeUnionConsumeProbe",6);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count,second])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count,second])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
        Assert.Contains("__unionSeen",generated);
    }
}
