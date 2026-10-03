using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// FLinq の契約: .Anchor の無い式は副作用なしとみなして評価の回数を変えてよく、.Anchor を書いた式は System.Linq と同じ回数だけ評価する。
/// 範囲の形から答えを出す近道(All/Any/Count の述語)と、列挙せずに件数を出す近道(Select の後の Count)が .Anchor を守ることを確かめる。
/// 「省いてよい」側は、FLinq が実際に省く形(Range の比較述語)だけを検査する(Select の後の Count は今は省かずに列挙する)。
/// </summary>
[LinqIntercept(false)]
public sealed class AnchorShortcutTests {
    private const string Source = """
        using System;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        public sealed class Sink {
            public object? Value;
            public void Consume<T>(T value) => this.Value=value;
        }
        public sealed class Container {
            public static int Calls;
            [LinqIntercept(false)]
            private static int Threshold() {
                Calls++;
                return 3;
            }
            [LinqIntercept(false)]
            private static int Mark(int value) {
                Calls++;
                return value;
            }
            public static string RunCountAnchored(int count) => CountAnchored(count).ToString();
            public static string RunCountPlain(int count) => CountPlain(count).ToString();
            public static string RunAnyAnchored(int count) => AnyAnchored(count).ToString();
            public static string RunAllAnchored(int count) => AllAnchored(count).ToString();
            public static string RunSelectCountAnchored(int count) {
                var sink = new Sink();
                SelectCountAnchored(new int[count],sink);
                return sink.Value!.ToString()!;
            }
            public static string RunSelectCountPlain(int count) {
                var sink = new Sink();
                SelectCountPlain(new int[count],sink);
                return sink.Value!.ToString()!;
            }

            [LinqIntercept(true)]
            private static int CountAnchored(int count) => System.Linq.Enumerable.Range(1,count).Count(x => x>=Threshold().Anchor);
            [LinqIntercept(true)]
            private static int CountPlain(int count) => System.Linq.Enumerable.Range(1,count).Count(x => x>=Threshold());
            [LinqIntercept(true)]
            private static bool AnyAnchored(int count) => System.Linq.Enumerable.Range(1,count).Any(x => x>=Threshold().Anchor);
            [LinqIntercept(true)]
            private static bool AllAnchored(int count) => System.Linq.Enumerable.Range(1,count).All(x => x>=Threshold().Anchor);
            // 終端の結果を引数として渡す形(ベンチの consumer.Consume(….Count()) と同じ経路)。
            [LinqIntercept(true)]
            private static void SelectCountAnchored(int[] values,Sink sink) => sink.Consume(values.Select(x => Mark(x).Anchor).Count());
            [LinqIntercept(true)]
            private static void SelectCountPlain(int[] values,Sink sink) => sink.Consume(values.Select(x => Mark(x)).Count());

        }
        """;

    [Theory]
    [InlineData("CountAnchored","3",5)]
    [InlineData("AnyAnchored","True",3)]
    [InlineData("AllAnchored","False",1)]
    [InlineData("SelectCountAnchored","5",5)]
    public void Anchorを書いた式はSystem_Linqと同じ回数だけ評価する(string method,string expected,int calls) {
        var (actual,actualCalls) = Run(method);
        Assert.Equal(expected,actual);
        Assert.Equal(calls,actualCalls);
    }

    [Theory]
    [InlineData("CountPlain","3")]
    public void Anchorの無い式は評価を省いてよい(string method,string expected) {
        var (actual,calls) = Run(method);
        Assert.Equal(expected,actual);
        // 結果は同じで、評価は System.Linq より少なくてよい(要素数 5 より少ない)。
        Assert.True(calls<5,"評価回数="+calls);
    }

    private static (string Result,int Calls) Run(string method) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"AnchorShortcutProbe",6);
        return ExecutableGeneration.Run(compilation,"Sample.Container",container => {
            var result = (string)container.GetMethod("Run"+method)!.Invoke(null,[5])!;
            return (result,(int)container.GetField("Calls")!.GetValue(null)!);
        });
    }
}
