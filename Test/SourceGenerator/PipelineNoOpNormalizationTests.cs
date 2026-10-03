using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 段をまとめる経路の正規化が System.Linq と同じ結果を返すこと。
/// - Range の先頭の Distinct・恒等キーの Order/OrderDescending(続く ThenBy を含む)・Skip(0)・Reverse を除く、または降順の Range にする。
/// - SelectMany(x => Enumerable.Repeat(e,1)[,結果選択子]) を Select にする。
/// - OrderBy(x => x) を Order() と同じ経路にする。
/// </summary>
[LinqIntercept(false)]
public sealed class PipelineNoOpNormalizationTests {
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
            private static string Describe<T>(Func<List<T>> evaluate) {
                try {
                    return string.Join(",",evaluate());
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
            }
            public static string RunFLinq(int s,int c,int[] values) => string.Join("|",
                Collect(sink => F.OrderWhere(s,c,sink)),
                Collect(sink => F.DistinctWhere(s,c,sink)),
                Collect(sink => F.SkipOrderWhere(s,c,sink)),
                Collect(sink => F.ReverseWhere(s,c,sink)),
                Collect(sink => F.OrderDescendingSelect(s,c,sink)),
                Collect(sink => F.OrderByThenBySelect(s,c,sink)),
                Collect(sink => F.ReverseReverseWhere(s,c,sink)),
                Describe(() => F.OrderDescendingTakeList(s,c)),
                Collect(sink => F.RepeatResult(values,sink)),
                Collect(sink => F.RepeatOnly(values,sink)),
                Collect(sink => F.RepeatTwice(values,sink)),
                Collect(sink => F.IdentityOrderBy(values,sink)),
                Collect(sink => F.IdentityOrderByDescendingThenBy(values,sink)));
            public static string RunLinq(int s,int c,int[] values) => string.Join("|",
                Collect(sink => L.OrderWhere(s,c,sink)),
                Collect(sink => L.DistinctWhere(s,c,sink)),
                Collect(sink => L.SkipOrderWhere(s,c,sink)),
                Collect(sink => L.ReverseWhere(s,c,sink)),
                Collect(sink => L.OrderDescendingSelect(s,c,sink)),
                Collect(sink => L.OrderByThenBySelect(s,c,sink)),
                Collect(sink => L.ReverseReverseWhere(s,c,sink)),
                Describe(() => L.OrderDescendingTakeList(s,c)),
                Collect(sink => L.RepeatResult(values,sink)),
                Collect(sink => L.RepeatOnly(values,sink)),
                Collect(sink => L.RepeatTwice(values,sink)),
                Collect(sink => L.IdentityOrderBy(values,sink)),
                Collect(sink => L.IdentityOrderByDescendingThenBy(values,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void OrderWhere(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Order().Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void DistinctWhere(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Distinct().Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void SkipOrderWhere(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Skip(0).Order().Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void ReverseWhere(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Reverse().Where(x => (x&1)==0));
                [LinqIntercept(true)] public static void OrderDescendingSelect(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderDescending().Select(x => x*3));
                [LinqIntercept(true)] public static void OrderByThenBySelect(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).OrderBy(x => x).ThenBy(x => -x).Select(x => x+1));
                [LinqIntercept(true)] public static void ReverseReverseWhere(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Reverse().Reverse().Where(x => x%3==0));
                [LinqIntercept(true)] public static List<int> OrderDescendingTakeList(int s,int c) => Enumerable.Range(s,c).OrderDescending().Take(3).ToList();
                [LinqIntercept(true)] public static void RepeatResult(int[] values,Sink sink) => sink.Consume(values.SelectMany(x => Enumerable.Repeat(x*2,1),(x,y) => x*100+y+y).Select(p => p+1));
                [LinqIntercept(true)] public static void RepeatOnly(int[] values,Sink sink) => sink.Consume(values.SelectMany(x => Enumerable.Repeat((long)x,1)).Where(v => v!=3));
                [LinqIntercept(true)] public static void RepeatTwice(int[] values,Sink sink) => sink.Consume(values.SelectMany(x => Enumerable.Repeat(x,2)).Select(v => v-1));
                [LinqIntercept(true)] public static void IdentityOrderBy(int[] values,Sink sink) => sink.Consume(values.OrderBy(x => x).Select(v => v*10));
                [LinqIntercept(true)] public static void IdentityOrderByDescendingThenBy(int[] values,Sink sink) => sink.Consume(values.OrderByDescending(x => x).ThenBy(x => x%2).Select(v => v));
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void OrderWhere(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Order().Where(x => (x&1)==0)) sink.Consume(v); }
                public static void DistinctWhere(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Distinct().Where(x => (x&1)==0)) sink.Consume(v); }
                public static void SkipOrderWhere(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Skip(0).Order().Where(x => (x&1)==0)) sink.Consume(v); }
                public static void ReverseWhere(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Reverse().Where(x => (x&1)==0)) sink.Consume(v); }
                public static void OrderDescendingSelect(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderDescending().Select(x => x*3)) sink.Consume(v); }
                public static void OrderByThenBySelect(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).OrderBy(x => x).ThenBy(x => -x).Select(x => x+1)) sink.Consume(v); }
                public static void ReverseReverseWhere(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Reverse().Reverse().Where(x => x%3==0)) sink.Consume(v); }
                public static List<int> OrderDescendingTakeList(int s,int c) => Enumerable.Range(s,c).OrderDescending().Take(3).ToList();
                public static void RepeatResult(int[] values,Sink sink) { foreach(var v in values.SelectMany(x => Enumerable.Repeat(x*2,1),(x,y) => x*100+y+y).Select(p => p+1)) sink.Consume(v); }
                public static void RepeatOnly(int[] values,Sink sink) { foreach(var v in values.SelectMany(x => Enumerable.Repeat((long)x,1)).Where(v => v!=3)) sink.Consume(v); }
                public static void RepeatTwice(int[] values,Sink sink) { foreach(var v in values.SelectMany(x => Enumerable.Repeat(x,2)).Select(v => v-1)) sink.Consume(v); }
                public static void IdentityOrderBy(int[] values,Sink sink) { foreach(var v in values.OrderBy(x => x).Select(v => v*10)) sink.Consume(v); }
                public static void IdentityOrderByDescendingThenBy(int[] values,Sink sink) { foreach(var v in values.OrderByDescending(x => x).ThenBy(x => x%2).Select(v => v)) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,10)]
    [InlineData(-5,9)]
    [InlineData(1,0)]
    [InlineData(int.MaxValue-4,5)]
    [InlineData(1,-1)]
    public void System_Linqと同じ結果を返す(int start,int count) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"PipelineNoOpNormalizationProbe",13);
        int[] values = [5,3,9,3,-1,0,7];
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count,values])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count,values])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }

    [Fact]
    public void Rangeの何もしない段は並べ替えも集合も作らない() {
        var (_,generated) = ExecutableGeneration.Generate(Source,"PipelineNoOpNormalizationShape",13);
        // Range の並べ替え・重複除きは残らない(並べ替えの比較も集合も作らない)。
        Assert.DoesNotContain("HashSetSlim",generated);
    }
}
