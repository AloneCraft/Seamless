using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Enumerable.Range の引数が不正(count&lt;0、start+count-1 が int を超える)なとき、FLinq が Range を数式で扱う経路
/// (個数・端・添字・集計・Consume への直接展開など)でも System.Linq と同じく ArgumentOutOfRangeException になること。
/// </summary>
[LinqIntercept(false)]
public sealed class RangeArgumentValidationTests {
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
            private static string D<T>(Func<T> evaluate) {
                try {
                    var value = evaluate();
                    return value is System.Collections.IEnumerable values and not string ? string.Join(",",values.Cast<object>()) : Convert.ToString(value)!;
                } catch(Exception exception) {
                    return "!"+exception.GetType().Name;
                }
            }
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
                D(() => F.Count(s,c)),D(() => F.Any(s,c)),D(() => F.First(s,c)),D(() => F.FirstOrDefault(s,c)),
                D(() => F.Last(s,c)),D(() => F.LastOrDefault(s,c)),D(() => F.ElementAt(s,c)),D(() => F.ElementAtOrDefault(s,c)),
                D(() => F.Contains(s,c)),D(() => F.ToArray(s,c)),D(() => F.ToList(s,c)),D(() => F.ToHashSet(s,c)),
                D(() => F.ToDictionary(s,c)),D(() => F.SequenceEqual(s,c)),D(() => F.Sum(s,c)),D(() => F.Max(s,c)),
                C(sink => F.Select(s,c,sink)),C(sink => F.Where(s,c,sink)),C(sink => F.Reverse(s,c,sink)),C(sink => F.TakeLast(s,c,sink)),
                C(sink => F.TakeWhile(s,c,sink)),C(sink => F.DefaultIfEmpty(s,c,sink)),C(sink => F.Concat(s,c,sink)),C(sink => F.Chunk(s,c,sink)),
                C(sink => F.Order(s,c,sink)),C(sink => F.Distinct(s,c,sink)));
            public static string RunLinq(int s,int c) => string.Join("|",
                D(() => L.Count(s,c)),D(() => L.Any(s,c)),D(() => L.First(s,c)),D(() => L.FirstOrDefault(s,c)),
                D(() => L.Last(s,c)),D(() => L.LastOrDefault(s,c)),D(() => L.ElementAt(s,c)),D(() => L.ElementAtOrDefault(s,c)),
                D(() => L.Contains(s,c)),D(() => L.ToArray(s,c)),D(() => L.ToList(s,c)),D(() => L.ToHashSet(s,c)),
                D(() => L.ToDictionary(s,c)),D(() => L.SequenceEqual(s,c)),D(() => L.Sum(s,c)),D(() => L.Max(s,c)),
                C(sink => L.Select(s,c,sink)),C(sink => L.Where(s,c,sink)),C(sink => L.Reverse(s,c,sink)),C(sink => L.TakeLast(s,c,sink)),
                C(sink => L.TakeWhile(s,c,sink)),C(sink => L.DefaultIfEmpty(s,c,sink)),C(sink => L.Concat(s,c,sink)),C(sink => L.Chunk(s,c,sink)),
                C(sink => L.Order(s,c,sink)),C(sink => L.Distinct(s,c,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static int Count(int s,int c) => Enumerable.Range(s,c).Count();
                [LinqIntercept(true)] public static bool Any(int s,int c) => Enumerable.Range(s,c).Any();
                [LinqIntercept(true)] public static int First(int s,int c) => Enumerable.Range(s,c).First();
                [LinqIntercept(true)] public static int FirstOrDefault(int s,int c) => Enumerable.Range(s,c).FirstOrDefault();
                [LinqIntercept(true)] public static int Last(int s,int c) => Enumerable.Range(s,c).Last();
                [LinqIntercept(true)] public static int LastOrDefault(int s,int c) => Enumerable.Range(s,c).LastOrDefault();
                [LinqIntercept(true)] public static int ElementAt(int s,int c) => Enumerable.Range(s,c).ElementAt(0);
                [LinqIntercept(true)] public static int ElementAtOrDefault(int s,int c) => Enumerable.Range(s,c).ElementAtOrDefault(0);
                [LinqIntercept(true)] public static bool Contains(int s,int c) => Enumerable.Range(s,c).Contains(1);
                [LinqIntercept(true)] public static int[] ToArray(int s,int c) => Enumerable.Range(s,c).ToArray();
                [LinqIntercept(true)] public static List<int> ToList(int s,int c) => Enumerable.Range(s,c).ToList();
                [LinqIntercept(true)] public static HashSet<int> ToHashSet(int s,int c) => Enumerable.Range(s,c).ToHashSet();
                [LinqIntercept(true)] public static int ToDictionary(int s,int c) => Enumerable.Range(s,c).ToDictionary(x => x).Count;
                [LinqIntercept(true)] public static bool SequenceEqual(int s,int c) => Enumerable.Range(s,c).SequenceEqual(Enumerable.Range(1,1));
                [LinqIntercept(true)] public static int Sum(int s,int c) => Enumerable.Range(s,c).Sum();
                [LinqIntercept(true)] public static int Max(int s,int c) => Enumerable.Range(s,c).Max();
                [LinqIntercept(true)] public static void Select(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Select(x => x+1));
                [LinqIntercept(true)] public static void Where(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Where(x => x>0));
                [LinqIntercept(true)] public static void Reverse(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Reverse());
                [LinqIntercept(true)] public static void TakeLast(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).TakeLast(2));
                [LinqIntercept(true)] public static void TakeWhile(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).TakeWhile(x => x<3));
                [LinqIntercept(true)] public static void DefaultIfEmpty(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).DefaultIfEmpty(9));
                [LinqIntercept(true)] public static void Concat(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Concat(Enumerable.Range(1,1)));
                [LinqIntercept(true)] public static void Chunk(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Chunk(2).Select(x => x.Length));
                [LinqIntercept(true)] public static void Order(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Order());
                [LinqIntercept(true)] public static void Distinct(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).Distinct());
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static int Count(int s,int c) => Enumerable.Range(s,c).Count();
                public static bool Any(int s,int c) => Enumerable.Range(s,c).Any();
                public static int First(int s,int c) => Enumerable.Range(s,c).First();
                public static int FirstOrDefault(int s,int c) => Enumerable.Range(s,c).FirstOrDefault();
                public static int Last(int s,int c) => Enumerable.Range(s,c).Last();
                public static int LastOrDefault(int s,int c) => Enumerable.Range(s,c).LastOrDefault();
                public static int ElementAt(int s,int c) => Enumerable.Range(s,c).ElementAt(0);
                public static int ElementAtOrDefault(int s,int c) => Enumerable.Range(s,c).ElementAtOrDefault(0);
                public static bool Contains(int s,int c) => Enumerable.Range(s,c).Contains(1);
                public static int[] ToArray(int s,int c) => Enumerable.Range(s,c).ToArray();
                public static List<int> ToList(int s,int c) => Enumerable.Range(s,c).ToList();
                public static HashSet<int> ToHashSet(int s,int c) => Enumerable.Range(s,c).ToHashSet();
                public static int ToDictionary(int s,int c) => Enumerable.Range(s,c).ToDictionary(x => x).Count;
                public static bool SequenceEqual(int s,int c) => Enumerable.Range(s,c).SequenceEqual(Enumerable.Range(1,1));
                public static int Sum(int s,int c) => Enumerable.Range(s,c).Sum();
                public static int Max(int s,int c) => Enumerable.Range(s,c).Max();
                public static void Select(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Select(x => x+1)) sink.Consume(v); }
                public static void Where(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Where(x => x>0)) sink.Consume(v); }
                public static void Reverse(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Reverse()) sink.Consume(v); }
                public static void TakeLast(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).TakeLast(2)) sink.Consume(v); }
                public static void TakeWhile(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).TakeWhile(x => x<3)) sink.Consume(v); }
                public static void DefaultIfEmpty(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).DefaultIfEmpty(9)) sink.Consume(v); }
                public static void Concat(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Concat(Enumerable.Range(1,1))) sink.Consume(v); }
                public static void Chunk(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Chunk(2).Select(x => x.Length)) sink.Consume(v); }
                public static void Order(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Order()) sink.Consume(v); }
                public static void Distinct(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).Distinct()) sink.Consume(v); }
            }
        }
        """;

    [Theory]
    [InlineData(1,-1)]
    [InlineData(int.MaxValue,2)]
    [InlineData(1,3)]
    [InlineData(5,0)]
    public void System_Linqと同じ結果と例外を返す(int start,int count) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"RangeArgumentValidationProbe",20);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }
}
