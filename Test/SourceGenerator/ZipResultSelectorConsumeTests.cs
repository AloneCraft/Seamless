using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Zip(second,resultSelector).Consume を、Zip の列挙子の構造体を作らずに差し替えメソッドで両方の根を直接回して流すこと。
/// 結果と、first が尽きたら second を進めない順(System.Linq と同じ)を、参照型の要素と int、span の有無の組で確かめる。
/// </summary>
[LinqIntercept(false)]
public sealed class ZipResultSelectorConsumeTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Item(int value) {
            public int Value { get; } = value;
            public override string ToString() => this.Value.ToString();
        }
        public sealed class Sink {
            public readonly List<string> Values = new();
            public void Consume<T>(T value) => this.Values.Add(value!.ToString()!);
        }
        public abstract class Holder<T> where T : notnull {
            public int SecondMoves;
            public abstract T[] Long();
            public abstract T[] Short();
            public static int N(T value) => value is Item item ? item.Value : (int)(object)value;
            public IEnumerable<T> Lazy(T[] values) {
                foreach(var value in values) {
                    this.SecondMoves++;
                    yield return value;
                }
            }
            public string RunFLinq() {
                var results = new List<string>();
                foreach(var action in new Action<Sink>[] {
                    sink => this.SpanSpan(sink),sink => this.SpanShort(sink),sink => this.LazyLazy(sink),sink => this.ShortLazy(sink),sink => this.Captured(sink,7),
                }) {
                    var sink = new Sink();
                    this.SecondMoves=0;
                    action(sink);
                    results.Add(string.Join(",",sink.Values)+"/"+this.SecondMoves);
                }
                return string.Join("|",results);
            }
            [LinqIntercept(false)]
            public string RunLinq() {
                var results = new List<string>();
                foreach(var func in new Func<IEnumerable<object>>[] {
                    () => this.Long().Zip(this.Short(),(a,b) => N(a)*10+N(b)).Cast<object>(),
                    () => this.Short().Zip(this.Long(),(a,b) => (N(a)+N(b)).ToString()).Cast<object>(),
                    () => this.Lazy(this.Long()).Zip(this.Lazy(this.Short()),(a,b) => N(a)-N(b)).Cast<object>(),
                    () => this.Short().Zip(this.Lazy(this.Long()),(a,b) => (N(a),N(b))).Cast<object>(),
                    () => { var offset = 7; return this.Long().Zip(this.Short(),(a,b) => N(a)+N(b)+offset).Cast<object>(); },
                }) {
                    this.SecondMoves=0;
                    var values = func().Select(static value => value.ToString()!).ToList();
                    results.Add(string.Join(",",values)+"/"+this.SecondMoves);
                }
                return string.Join("|",results);
            }
            [LinqIntercept(true)] private void SpanSpan(Sink sink) => sink.Consume(this.Long().Zip(this.Short(),(a,b) => N(a)*10+N(b)));
            [LinqIntercept(true)] private void SpanShort(Sink sink) => sink.Consume(this.Short().Zip(this.Long(),(a,b) => (N(a)+N(b)).ToString()));
            [LinqIntercept(true)] private void LazyLazy(Sink sink) => sink.Consume(this.Lazy(this.Long()).Zip(this.Lazy(this.Short()),(a,b) => N(a)-N(b)));
            [LinqIntercept(true)] private void ShortLazy(Sink sink) => sink.Consume(this.Short().Zip(this.Lazy(this.Long()),(a,b) => (N(a),N(b))));
            [LinqIntercept(true)] private void Captured(Sink sink,int offset) => sink.Consume(this.Long().Zip(this.Short(),(a,b) => N(a)+N(b)+offset));
        }
        public sealed class ItemHolder:Holder<Item> {
            public override Item[] Long() => [new(5),new(1),new(3),new(8),new(2)];
            public override Item[] Short() => [new(4),new(9),new(6)];
        }
        public sealed class IntHolder:Holder<int> {
            public override int[] Long() => [5,1,3,8,2];
            public override int[] Short() => [4,9,6];
        }
        public sealed class Probe {
            public static string RunFLinq() => new ItemHolder().RunFLinq()+"#"+new IntHolder().RunFLinq();
            public static string RunLinq() => new ItemHolder().RunLinq()+"#"+new IntHolder().RunLinq();
        }
        """;

    [Fact]
    public void 両方の根を差し替えメソッドで直接回す() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"ZipResultSelectorConsumeProbe",5);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(expected.Split('|','#'),actual.Split('|','#'));
        // 汎用の入れ子の Consume(Zip の列挙子の構造体を TryGetNext で回す形)は残らない。
        Assert.DoesNotContain("while(ce",generated);
        Assert.Contains("zipSourceSpan",generated);
    }
}
