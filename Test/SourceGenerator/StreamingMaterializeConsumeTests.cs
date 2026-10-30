using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Range の ToHashSet()・恒等キーの ToDictionary を Consume へ流すだけのとき、集合・辞書を作らずに要素を直接流すこと。
/// 結果(順・値)と、重複キーの例外が流す前に起きること(恒等でないキー)は System.Linq と同じ。
/// </summary>
[LinqIntercept(false)]
public sealed class StreamingMaterializeConsumeTests {
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
                    return string.Join(",",sink.Values)+"!"+exception.GetType().Name;
                }
                return string.Join(",",sink.Values);
            }
            public static string RunFLinq(int s,int c) => string.Join("|",
                C(sink => F.HashSet(s,c,sink)),C(sink => F.Dictionary(s,c,sink)),C(sink => F.DictionaryElement(s,c,sink)),
                C(sink => F.DictionaryDuplicate(s,c,sink)),C(sink => F.ReceiverHashSet(s,c,sink)));
            public static string RunLinq(int s,int c) => string.Join("|",
                C(sink => L.HashSet(s,c,sink)),C(sink => L.Dictionary(s,c,sink)),C(sink => L.DictionaryElement(s,c,sink)),
                C(sink => L.DictionaryDuplicate(s,c,sink)),C(sink => L.ReceiverHashSet(s,c,sink)));
            public sealed class F {
                [LinqIntercept(true)] public static void HashSet(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).ToHashSet());
                [LinqIntercept(true)] public static void Dictionary(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).ToDictionary(x => x));
                [LinqIntercept(true)] public static void DictionaryElement(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).ToDictionary(x => x,x => x*2));
                [LinqIntercept(true)] public static void DictionaryDuplicate(int s,int c,Sink sink) => sink.Consume(Enumerable.Range(s,c).ToDictionary(x => x%3));
                [LinqIntercept(true)] public static void ReceiverHashSet(int s,int c,Sink sink) => Enumerable.Range(s,c).ToHashSet().Consume(sink);
            }
            [LinqIntercept(false)]
            public sealed class L {
                public static void HashSet(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).ToHashSet()) sink.Consume(v); }
                public static void Dictionary(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).ToDictionary(x => x)) sink.Consume(v); }
                public static void DictionaryElement(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).ToDictionary(x => x,x => x*2)) sink.Consume(v); }
                public static void DictionaryDuplicate(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).ToDictionary(x => x%3)) sink.Consume(v); }
                public static void ReceiverHashSet(int s,int c,Sink sink) { foreach(var v in Enumerable.Range(s,c).ToHashSet()) sink.Consume(v); }
            }
        }
        public static class SinkExtensions {
            public static void Consume<T>(this IEnumerable<T> source,Sink sink) {
                foreach(var value in source) {
                    sink.Consume(value);
                }
            }
        }
        """;

    [Theory]
    [InlineData(1,6)]
    [InlineData(-3,4)]
    [InlineData(1,0)]
    [InlineData(1,-1)]
    public void System_Linqと同じ結果を返す(int start,int count) {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"StreamingMaterializeConsumeProbe",5);
        // 恒等キーの辞書2つは作らずに (x, 値) を流す(x%3 のキーの辞書は作る)。
        Assert.Equal(2,global::System.Text.RegularExpressions.Regex.Matches(generated,@"KeyValuePair<int,int>\(value").Count);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,[start,count])!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,[start,count])!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
    }
}
