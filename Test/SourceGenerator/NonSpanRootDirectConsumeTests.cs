using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// span の無い根(yield の IEnumerable)の単段演算子を Consume へ流すとき、演算子の列挙子の構造体を回さず、
/// 根の列挙子を差し替えメソッドの中から直接回すこと(参照型の要素の共有ジェネリックコードで入れ子がインライン化されないため)。
/// 結果は System.Linq と同じ。
/// </summary>
[LinqIntercept(false)]
public sealed class NonSpanRootDirectConsumeTests {
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
            public abstract IEnumerable<T> Source();
            // Append/Prepend の値はインスタンスのメンバーを使わない(既存の不具合: 構造体の中で @this が見えずコンパイルできない)。
            public static T Extra() => (T)(object)new Item(99);
            public static int N(T value) => value is Item item ? item.Value : (int)(object)value;
            public string RunFLinq() {
                var results = new List<string>();
                foreach(var action in new Action<Sink>[] {
                    sink => this.Where(sink),sink => this.WhereIndex(sink),sink => this.SelectIndex(sink),sink => this.SkipLast(sink),
                    sink => this.TakeLast(sink),sink => this.Reverse(sink),sink => this.Append(sink),sink => this.Prepend(sink),
                    sink => this.TakeWhile(sink),sink => this.SkipWhileIndex(sink),sink => this.GroupJoin(sink),
                }) {
                    var sink = new Sink();
                    action(sink);
                    results.Add(string.Join(",",sink.Values));
                }
                return string.Join("|",results);
            }
            [LinqIntercept(false)]
            public string RunLinq() {
                var source = this.Source();
                return string.Join("|",new IEnumerable<object>[] {
                    source.Where(x => N(x)%2==0).Cast<object>(),
                    source.Where((x,i) => i%3!=0).Cast<object>(),
                    source.Select((x,i) => N(x)*10+i).Cast<object>(),
                    source.SkipLast(2).Cast<object>(),
                    source.TakeLast(3).Cast<object>(),
                    source.Reverse().Cast<object>(),
                    source.Append(Extra()).Cast<object>(),
                    source.Prepend(Extra()).Cast<object>(),
                    source.TakeWhile(x => N(x)<4).Cast<object>(),
                    source.SkipWhile((x,i) => i<2).Cast<object>(),
                    source.GroupJoin(this.Source(),x => x,x => x,(o,g) => o).Cast<object>(),
                }.Select(values => string.Join(",",values)));
            }
            [LinqIntercept(true)] private void Where(Sink sink) => sink.Consume(this.Source().Where(x => N(x)%2==0));
            [LinqIntercept(true)] private void WhereIndex(Sink sink) => sink.Consume(this.Source().Where((x,i) => i%3!=0));
            [LinqIntercept(true)] private void SelectIndex(Sink sink) => sink.Consume(this.Source().Select((x,i) => N(x)*10+i));
            [LinqIntercept(true)] private void SkipLast(Sink sink) => sink.Consume(this.Source().SkipLast(2));
            [LinqIntercept(true)] private void TakeLast(Sink sink) => sink.Consume(this.Source().TakeLast(3));
            [LinqIntercept(true)] private void Reverse(Sink sink) => sink.Consume(this.Source().Reverse());
            [LinqIntercept(true)] private void Append(Sink sink) => sink.Consume(this.Source().Append(Extra()));
            [LinqIntercept(true)] private void Prepend(Sink sink) => sink.Consume(this.Source().Prepend(Extra()));
            [LinqIntercept(true)] private void TakeWhile(Sink sink) => sink.Consume(this.Source().TakeWhile(x => N(x)<4));
            [LinqIntercept(true)] private void SkipWhileIndex(Sink sink) => sink.Consume(this.Source().SkipWhile((x,i) => i<2));
            [LinqIntercept(true)] private void GroupJoin(Sink sink) => sink.Consume(this.Source().GroupJoin(this.Source(),x => x,x => x,(o,g) => o));
        }
        public sealed class ItemHolder:Holder<Item> {
            private readonly Item[] items = [new(1),new(2),new(3),new(4),new(5),new(6),new(7)];
            public override IEnumerable<Item> Source() {
                foreach(var item in this.items) {
                    yield return item;
                }
            }
        }
        public sealed class Probe {
            public static string RunFLinq() => new ItemHolder().RunFLinq();
            public static string RunLinq() => new ItemHolder().RunLinq();
        }
        """;

    [Fact]
    public void 根の列挙子を差し替えメソッドから直接回す() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"NonSpanRootDirectConsumeProbe",11);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
        // 演算子の列挙子の構造体を回す汎用の Consume(using var __ce… / while(__ce…)は残らない。
        Assert.DoesNotContain("while(__ce",generated);
    }
}
