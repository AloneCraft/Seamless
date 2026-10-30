using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range の SkipLast を Consume へ流すとき、待ち行列を使わず先頭からの短い Range として流す変換が System.Linq と同じ結果・例外を返すこと。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeSkipLastTests {
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
            private static string Collect(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            public static string RunFLinq(int s,int c,int k) => string.Join("|",
                Collect(sink => F.Once(s,c,k,sink)),
                Collect(sink => F.Twice(s,c,k,sink)));
            public static string RunLinq(int s,int c,int k) => string.Join("|",
                Collect(sink => L.Once(s,c,k,sink)),
                Collect(sink => L.Twice(s,c,k,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void Once(int s,int c,int k,Sink sink) => sink.Consume(Enumerable.Range(s,c).SkipLast(k));
                [LinqIntercept(true)] public static void Twice(int s,int c,int k,Sink sink) => sink.Consume(Enumerable.Range(s,c).SkipLast(k).SkipLast(2));
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void Once(int s,int c,int k,Sink sink) { foreach(var v in Enumerable.Range(s,c).SkipLast(k)) sink.Consume(v); }
                public static void Twice(int s,int c,int k,Sink sink) { foreach(var v in Enumerable.Range(s,c).SkipLast(k).SkipLast(2)) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,10,3)]
    [InlineData(1,10,0)]
    [InlineData(1,10,-4)]
    [InlineData(1,10,10)]
    [InlineData(1,10,25)]
    [InlineData(-5,4,1)]
    [InlineData(1,0,1)]
    [InlineData(int.MaxValue-4,5,2)]
    [InlineData(1,-1,1)]
    public void System_Linqと同じ結果を返す(int start,int count,int skip) {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"RangeSkipLastProbe",2);
        Assert.Equal(2,global::System.Text.RegularExpressions.Regex.Matches(generated,@"var skipLastKeep\d*=").Count);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count,skip])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count,skip])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }
}
