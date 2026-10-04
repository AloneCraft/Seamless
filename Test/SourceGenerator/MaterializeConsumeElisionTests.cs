using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Consume へ渡すだけの ToArray()/ToList() は、.Anchor が無ければ作らずに元の列を直接流す(要素と順は同じ)。
/// 作らないことは、1件目で止める Consume で元の列の評価回数が減ることで確かめる。
/// .Anchor 付きと、途中で例外を投げ得る Cast を含む列は従来どおり作ってから流す。
/// </summary>
[LinqIntercept(false)]
public sealed class MaterializeConsumeElisionTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        public sealed class Sink(bool stopAfterFirst) {
            public readonly List<string> Values = new();
            public void Consume<T>(T value) {
                this.Values.Add(value!.ToString()!);
                if(stopAfterFirst) {
                    throw new OperationCanceledException();
                }
            }
            public string Drain(Action<Sink> action) {
                try {
                    action(this);
                } catch(OperationCanceledException) {
                }
                return string.Join(",",this.Values);
            }
        }
        public sealed class Container {
            public static int Calls;
            private static readonly int[] Values = [1,2,3,4,5,6,7];
            [LinqIntercept(false)]
            private static int Mark(int value) {
                Calls++;
                return value;
            }
            public static string RunArray(bool stop) => new Sink(stop).Drain(static sink => Array(sink));
            public static string RunList(bool stop) => new Sink(stop).Drain(static sink => List(sink));
            public static string RunReceiver(bool stop) => new Sink(stop).Drain(static sink => Receiver(sink));
            public static string RunAnchored(bool stop) => new Sink(stop).Drain(static sink => Anchored(sink));
            public static string RunCast(bool stop) => new Sink(stop).Drain(static sink => Cast(sink));
            public static string RunStaticCall(bool stop) => new Sink(stop).Drain(static sink => StaticCall(sink));

            [LinqIntercept(true)]
            private static void Array(Sink sink) => sink.Consume(Values.Select(x => Mark(x)*10).ToArray());
            [LinqIntercept(true)]
            private static void List(Sink sink) => sink.Consume(Values.Where(x => Mark(x)%2==1).ToList());
            [LinqIntercept(true)]
            private static void Receiver(Sink sink) => Values.Select(x => Mark(x)+1).ToArray().Consume(sink);
            [LinqIntercept(true)]
            private static void Anchored(Sink sink) => sink.Consume(Values.Select(x => Mark(x).Anchor*10).ToArray());
            [LinqIntercept(true)]
            private static void Cast(Sink sink) => sink.Consume(Values.Select(x => (object)Mark(x)).Cast<int>().ToArray());
            // 静的呼び出しの形(List<T>.ToArray を避けるために書く形)も同じく作らない。
            [LinqIntercept(true)]
            private static void StaticCall(Sink sink) => System.Linq.Enumerable.ToArray(Values.Select(x => Mark(x)-1)).Consume(sink);
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
    // 全件流すと結果は同じ。
    [InlineData("Array",false,"10,20,30,40,50,60,70",7)]
    [InlineData("List",false,"1,3,5,7",7)]
    [InlineData("Receiver",false,"2,3,4,5,6,7,8",7)]
    [InlineData("Anchored",false,"10,20,30,40,50,60,70",7)]
    [InlineData("Cast",false,"1,2,3,4,5,6,7",7)]
    [InlineData("StaticCall",false,"0,1,2,3,4,5,6",7)]
    // 1件目で止めると、作らずに流す形は元の列を1件分だけ評価する。
    [InlineData("Array",true,"10",1)]
    [InlineData("List",true,"1",1)]
    [InlineData("Receiver",true,"2",1)]
    [InlineData("StaticCall",true,"0",1)]
    // .Anchor 付きと Cast を含む列は作ってから流す(全件評価してから1件目で止まる)。
    [InlineData("Anchored",true,"10",7)]
    [InlineData("Cast",true,"1",7)]
    public void 流すだけのToArrayとToListは作らない(string method,bool stop,string expected,int calls) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"MaterializeConsumeElisionProbe",6);
        var (actual,actualCalls) = ExecutableGeneration.Run(compilation,"Sample.Container",container => {
            var result = (string)container.GetMethod("Run"+method)!.Invoke(null,[stop])!;
            return (result,(int)container.GetField("Calls")!.GetValue(null)!);
        });
        Assert.Equal(expected,actual);
        Assert.Equal(calls,actualCalls);
    }
}
