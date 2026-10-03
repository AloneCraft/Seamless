using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range(昇順・降順)の先頭の Where が値か添字の偶奇だけを見るとき、述語を評価せずに2刻みで回す変換が
/// System.Linq と同じ結果・例外を返すこと。負の値・int の端・空・不正な引数、続く Select/Where を含める。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeParityWhereTests {
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
            private static string C(Action<Sink> action) {
                var sink = new Sink();
                try {
                    action(sink);
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            public static string RunFLinq(int s,int c) => string.Join("|",
                C(sink => F.Even(s,c,sink)),C(sink => F.Odd(s,c,sink)),C(sink => F.EvenIndex(s,c,sink)),C(sink => F.OddIndex(s,c,sink)),
                C(sink => F.ReverseEven(s,c,sink)),C(sink => F.ReverseOddIndex(s,c,sink)),C(sink => F.OrderOdd(s,c,sink)),C(sink => F.EvenSelect(s,c,sink)),
                C(sink => F.OddIndexWhere(s,c,sink)));
            public static string RunLinq(int s,int c) => string.Join("|",
                C(sink => L.Even(s,c,sink)),C(sink => L.Odd(s,c,sink)),C(sink => L.EvenIndex(s,c,sink)),C(sink => L.OddIndex(s,c,sink)),
                C(sink => L.ReverseEven(s,c,sink)),C(sink => L.ReverseOddIndex(s,c,sink)),C(sink => L.OrderOdd(s,c,sink)),C(sink => L.EvenSelect(s,c,sink)),
                C(sink => L.OddIndexWhere(s,c,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void Even(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void Odd(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where(x => (x&1)!=0));
                [LinqIntercept(true)] public static void EvenIndex(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where((x,i) => (i&1)==0));
                [LinqIntercept(true)] public static void OddIndex(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where((x,i) => (i&1)==1));
                [LinqIntercept(true)] public static void ReverseEven(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Reverse().Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void ReverseOddIndex(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Reverse().Where((x,i) => (i&1)!=0));
                [LinqIntercept(true)] public static void OrderOdd(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Order().Where(x => (x&1)==1));
                [LinqIntercept(true)] public static void EvenSelect(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where(x => (x&1)==0).Select(x => x*3));
                [LinqIntercept(true)] public static void OddIndexWhere(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where((x,i) => (i&1)==1).Where(x => x>2));
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void Even(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where(x => (x&1)==0)) sink.Consume(v); }
                public static void Odd(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where(x => (x&1)!=0)) sink.Consume(v); }
                public static void EvenIndex(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where((x,i) => (i&1)==0)) sink.Consume(v); }
                public static void OddIndex(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where((x,i) => (i&1)==1)) sink.Consume(v); }
                public static void ReverseEven(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Reverse().Where(x => (x&1)==0)) sink.Consume(v); }
                public static void ReverseOddIndex(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Reverse().Where((x,i) => (i&1)!=0)) sink.Consume(v); }
                public static void OrderOdd(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Order().Where(x => (x&1)==1)) sink.Consume(v); }
                public static void EvenSelect(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where(x => (x&1)==0).Select(x => x*3)) sink.Consume(v); }
                public static void OddIndexWhere(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where((x,i) => (i&1)==1).Where(x => x>2)) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,10)]
    [InlineData(2,9)]
    [InlineData(-7,12)]
    [InlineData(1,0)]
    [InlineData(5,1)]
    [InlineData(int.MaxValue-6,7)]
    [InlineData(int.MinValue,6)]
    [InlineData(1,-1)]
    public void System_Linqと同じ結果を返す(int start,int count) {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"RangeParityWhereProbe",9);
        Assert.Equal(9,global::System.Text.RegularExpressions.Regex.Matches(generated,@"__parityValue\w*[+-]=2\)").Count);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }
}
