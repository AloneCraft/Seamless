using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 同じソース式の根どうしの SequenceEqual は、.Anchor が無ければ(純粋・決定的の契約)比べずに true にする。
/// 違う式、.Anchor 付き、既定でない比較子では従来どおり要素を比べる。
/// </summary>
[LinqIntercept(false)]
public sealed class SameSourceSequenceEqualTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        public sealed class Sink {
            public object? Value;
            public void Consume<T>(T value) => this.Value=value;
        }
        public sealed class NeverEqual:IEqualityComparer<int> {
            public bool Equals(int x,int y) => false;
            public int GetHashCode(int value) => value;
        }
        public sealed class Container {
            public static int Calls;
            private readonly int[] values = [1,2,3];
            private readonly int[] others = [1,2,4];
            [LinqIntercept(false)]
            private int[] Values() {
                Calls++;
                return this.values;
            }
            [LinqIntercept(false)]
            private int[] Others() {
                Calls++;
                return this.others;
            }
            public static string RunSame() => Run(static (c,s) => c.Same(s));
            public static string RunDifferent() => Run(static (c,s) => c.Different(s));
            public static string RunAnchored() => Run(static (c,s) => c.Anchored(s));
            public static string RunDefaultComparer() => Run(static (c,s) => c.DefaultComparer(s));
            public static string RunCustomComparer() => Run(static (c,s) => c.CustomComparer(s));
            private static string Run(Action<Container,Sink> action) {
                var sink = new Sink();
                action(new Container(),sink);
                return sink.Value+"/"+Calls;
            }
            [LinqIntercept(true)]
            private void Same(Sink sink) => sink.Consume(this.Values().SequenceEqual(this.Values()));
            [LinqIntercept(true)]
            private void Different(Sink sink) => sink.Consume(this.Values().SequenceEqual(this.Others()));
            [LinqIntercept(true)]
            private void Anchored(Sink sink) => sink.Consume(this.Values().Anchor.SequenceEqual(this.Values()));
            [LinqIntercept(true)]
            private void DefaultComparer(Sink sink) => sink.Consume(this.Values().SequenceEqual(this.Values(),EqualityComparer<int>.Default));
            [LinqIntercept(true)]
            private void CustomComparer(Sink sink) => sink.Consume(this.Values().SequenceEqual(this.Values(),new NeverEqual()));
        }
        """;

    [Theory]
    // 同じ式は比べずに等しい(ソース式も評価しない)。
    [InlineData("Same","True/0")]
    [InlineData("DefaultComparer","True/0")]
    // 違う式は比べる。
    [InlineData("Different","False/2")]
    // .Anchor 付きは比べる(ソース式を2回評価する)。
    [InlineData("Anchored","True/2")]
    // 既定でない比較子は反射的とは限らないので比べる。
    [InlineData("CustomComparer","False/2")]
    public void 同じソース式の根どうしだけ比べずに等しい(string method,string expected) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"SameSourceSequenceEqualProbe",5);
        var actual = ExecutableGeneration.Run(compilation,"Sample.Container",container => (string)container.GetMethod("Run"+method)!.Invoke(null,null)!);
        Assert.Equal(expected,actual);
    }
}
