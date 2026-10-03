using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range の GroupBy(x => x%C[,要素選択子|グループを使わない結果選択子]) を Consume へ流すとき、剰余類からグループ(または結果)を直接作る変換が
/// System.Linq と同じグループ(キーの初出順・グループ内は元の順)と例外を返すこと。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeModuloGroupByTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Sink {
            public readonly List<string> Values = new();
            public void Consume<T>(T value) => this.Values.Add(value switch {
                IGrouping<int,int> group => group.Key+":["+string.Join(",",group)+"]",
                IGrouping<int,long> group => group.Key+":["+string.Join(",",group)+"]",
                _ => value!.ToString()!,
            });
        }
        public sealed class Probe {
            private static string C(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(" ",sink.Values);
            }
            public static string RunFLinq(int s,int c) => string.Join("|",
                C(sink => F.Keys7(s,c,sink)),C(sink => F.Elements7(s,c,sink)),C(sink => F.Keys1000(s,c,sink)),C(sink => F.Widen7(s,c,sink)),C(sink => F.Result7(s,c,sink)));
            public static string RunLinq(int s,int c) => string.Join("|",
                C(sink => L.Keys7(s,c,sink)),C(sink => L.Elements7(s,c,sink)),C(sink => L.Keys1000(s,c,sink)),C(sink => L.Widen7(s,c,sink)),C(sink => L.Result7(s,c,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void Keys7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).GroupBy(x => x%7));
                [LinqIntercept(true)] public static void Elements7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).GroupBy(x => x%7,x => x*2));
                [LinqIntercept(true)] public static void Keys1000(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).GroupBy(x => x%1000));
                [LinqIntercept(true)] public static void Widen7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).GroupBy(x => x%7,x => (long)x));
                [LinqIntercept(true)] public static void Result7(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).GroupBy(x => x%7,(key,g) => key*10+1));
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void Keys7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).GroupBy(x => x%7)) sink.Consume(v); }
                public static void Elements7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).GroupBy(x => x%7,x => x*2)) sink.Consume(v); }
                public static void Keys1000(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).GroupBy(x => x%1000)) sink.Consume(v); }
                public static void Widen7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).GroupBy(x => x%7,x => (long)x)) sink.Consume(v); }
                public static void Result7(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).GroupBy(x => x%7,(key,g) => key*10+1)) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,0)]
    [InlineData(1,5)]
    [InlineData(3,30)]
    [InlineData(1,4096)]
    [InlineData(-20,45)]
    [InlineData(-7,8)]
    [InlineData(int.MaxValue-10,11)]
    [InlineData(int.MinValue,10)]
    [InlineData(1,-1)]
    public void System_Linqと同じグループを返す(int start,int count) {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"RangeModuloGroupByProbe",5);
        Assert.Equal(4,global::System.Text.RegularExpressions.Regex.Matches(generated,@"new global::Seamless\.Linq\.Grouping<int,\w+>\(\(int\)\(").Count);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }
}
