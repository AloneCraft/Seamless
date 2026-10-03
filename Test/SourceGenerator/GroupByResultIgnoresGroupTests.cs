using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 結果選択子がグループを使わない GroupBy は、.Anchor が無ければグループを作らずキーの重複だけを除く(要素選択子は評価しない、結果は逐次流す)。
/// 結果は System.Linq と同じ(キーの初出順)。要素選択子・キー選択子・ソースに .Anchor があれば、従来どおり全件を読んでグループを作る。
/// 後ろに段が続く GroupBy は段をまとめる経路、単独の GroupBy は演算子ごとの経路を通るので、両方を確かめる。
/// </summary>
[LinqIntercept(false)]
public sealed class GroupByResultIgnoresGroupTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        // ベンチの consumer.Consume(列) と同じ形で、単独の GroupBy を差し替えた列挙子で直接流す。
        // stopAfterFirst のときは最初の1件で止める(後段が途中で止める場合の評価回数を見る)。
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
            public static string RunElementPlain() => string.Join(",",ElementPlain());
            public static string RunElementAnchored() => string.Join(",",ElementAnchored());
            public static string RunUsesGroup() => string.Join(",",UsesGroup());
            public static string RunFirstPlain() => FirstPlain().ToString();
            public static string RunFirstSourceAnchored() => FirstSourceAnchored().ToString();
            public static string RunFirstKeyAnchored() => FirstKeyAnchored().ToString();
            // 単独の GroupBy。
            public static string RunSingleElementPlain() => new Sink(false).Drain(static sink => SingleElementPlain(sink));
            public static string RunSingleFirstPlain() => new Sink(true).Drain(static sink => SingleFirstPlain(sink));
            public static string RunSingleFirstKeyAnchored() => new Sink(true).Drain(static sink => SingleFirstKeyAnchored(sink));

            [LinqIntercept(true)]
            private static int[] ElementPlain() => Values.GroupBy(x => x%3,x => Mark(x),(key,g) => key*10).ToArray();
            [LinqIntercept(true)]
            private static int[] ElementAnchored() => Values.GroupBy(x => x%3,x => Mark(x).Anchor,(key,g) => key*10).ToArray();
            [LinqIntercept(true)]
            private static int[] UsesGroup() => Values.GroupBy(x => x%3,x => Mark(x),(key,g) => key*10+g.Sum()).ToArray();
            [LinqIntercept(true)]
            private static int FirstPlain() => Values.Select(x => Mark(x)).GroupBy(x => x%3,(key,g) => key*10).First();
            [LinqIntercept(true)]
            private static int FirstSourceAnchored() => Values.Select(x => Mark(x)).Anchor.GroupBy(x => x%3,(key,g) => key*10).First();
            [LinqIntercept(true)]
            private static int FirstKeyAnchored() => Values.GroupBy(x => Mark(x).Anchor%3,(key,g) => key*10).First();
            [LinqIntercept(true)]
            private static void SingleElementPlain(Sink sink) => sink.Consume(Values.GroupBy(x => x%3,x => Mark(x),(key,g) => key*10));
            [LinqIntercept(true)]
            private static void SingleFirstPlain(Sink sink) => sink.Consume(Values.GroupBy(x => Mark(x)%3,(key,g) => key*10));
            [LinqIntercept(true)]
            private static void SingleFirstKeyAnchored(Sink sink) => sink.Consume(Values.GroupBy(x => Mark(x).Anchor%3,(key,g) => key*10));
        }
        """;

    [Theory]
    // 要素選択子は評価しない。
    [InlineData("ElementPlain","10,20,0",0)]
    // .Anchor を書いた要素選択子は全件評価する。
    [InlineData("ElementAnchored","10,20,0",7)]
    // グループを使う結果選択子は従来どおり(要素選択子も全件評価する)。
    [InlineData("UsesGroup","22,27,9",7)]
    // 結果を逐次流すので、First や1件目で止める Consume は最初のキーで止まる。
    [InlineData("FirstPlain","10",1)]
    // ソースやキー選択子に .Anchor があれば全件読む。
    [InlineData("FirstSourceAnchored","10",7)]
    [InlineData("FirstKeyAnchored","10",7)]
    [InlineData("SingleElementPlain","10,20,0",0)]
    [InlineData("SingleFirstPlain","10",1)]
    [InlineData("SingleFirstKeyAnchored","10",7)]
    public void 結果がグループを使わなければキーの重複だけを除く(string method,string expected,int calls) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"GroupByResultIgnoresGroupProbe",9);
        var (actual,actualCalls) = ExecutableGeneration.Run(compilation,"Sample.Container",container => {
            var result = (string)container.GetMethod("Run"+method)!.Invoke(null,null)!;
            return (result,(int)container.GetField("Calls")!.GetValue(null)!);
        });
        Assert.Equal(expected,actual);
        Assert.Equal(calls,actualCalls);
    }
}
