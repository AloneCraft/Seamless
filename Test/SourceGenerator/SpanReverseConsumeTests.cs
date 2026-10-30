using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// span を出せる根(配列・List)の後に、添字の無い Where/Select と Reverse が1つだけ並ぶとき、
/// 要素を溜めずに span を末尾から回して段をその場に展開すること。結果は System.Linq と同じ。
/// </summary>
[LinqIntercept(false)]
public sealed class SpanReverseConsumeTests {
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
            public abstract T[] Array();
            public List<T> List() => new(this.Array());
            public IEnumerable<T> AsEnumerable() => this.List();
            public IEnumerable<T> Lazy() {
                foreach(var value in this.Array()) {
                    yield return value;
                }
            }
            public static int N(T value) => value is Item item ? item.Value : (int)(object)value;
            public string RunFLinq() {
                var results = new List<string>();
                foreach(var action in new Action<Sink>[] {
                    sink => this.ReverseWhere(sink),sink => this.WhereReverse(sink),sink => this.SelectReverseWhere(sink),
                    sink => this.ListReverseSelect(sink),sink => this.ReverseOnlyList(sink),sink => this.EmptyReverseWhere(sink),
                    sink => this.EnumerableListReverseWhere(sink),sink => this.LazyWhereReverse(sink),
                }) {
                    var sink = new Sink();
                    action(sink);
                    results.Add(string.Join(",",sink.Values));
                }
                return string.Join("|",results);
            }
            [LinqIntercept(false)]
            public string RunLinq() => string.Join("|",new IEnumerable<object>[] {
                this.Array().Reverse().Where(x => N(x)%3!=0).Cast<object>(),
                this.Array().Where(x => N(x)>2).Reverse().Cast<object>(),
                this.Array().Select(x => N(x)*2).Reverse().Where(x => x%4==0).Cast<object>(),
                this.List().AsEnumerable().Reverse().Select(x => N(x)+100).Cast<object>(),
                this.List().Where(x => N(x)!=5).Reverse().Cast<object>(),
                System.Array.Empty<T>().Reverse().Where(x => N(x)>0).Cast<object>(),
                this.AsEnumerable().Reverse().Where(x => N(x)%3!=0).Cast<object>(),
                this.Lazy().Where(x => N(x)>2).Reverse().Select(x => N(x)*10).Cast<object>(),
            }.Select(values => string.Join(",",values)));
            [LinqIntercept(true)] private void ReverseWhere(Sink sink) => sink.Consume(this.Array().Reverse().Where(x => N(x)%3!=0));
            [LinqIntercept(true)] private void WhereReverse(Sink sink) => sink.Consume(this.Array().Where(x => N(x)>2).Reverse());
            [LinqIntercept(true)] private void SelectReverseWhere(Sink sink) => sink.Consume(this.Array().Select(x => N(x)*2).Reverse().Where(x => x%4==0));
            [LinqIntercept(true)] private void ListReverseSelect(Sink sink) => sink.Consume(Enumerable.Reverse(this.List()).Select(x => N(x)+100));
            [LinqIntercept(true)] private void ReverseOnlyList(Sink sink) => sink.Consume(this.List().Where(x => N(x)!=5).Reverse());
            // IEnumerable の根: 実行時に span を出せれば末尾から、出せなければ溜めてから末尾から回す。
            [LinqIntercept(true)] private void EnumerableListReverseWhere(Sink sink) => sink.Consume(this.AsEnumerable().Reverse().Where(x => N(x)%3!=0));
            [LinqIntercept(true)] private void LazyWhereReverse(Sink sink) => sink.Consume(this.Lazy().Where(x => N(x)>2).Reverse().Select(x => N(x)*10));
            [LinqIntercept(true)] private void EmptyReverseWhere(Sink sink) => sink.Consume(System.Array.Empty<T>().Reverse().Where(x => N(x)>0));
        }
        public sealed class ItemHolder:Holder<Item> {
            public override Item[] Array() => [new(5),new(1),new(3),new(5),new(2),new(8),new(1),new(6),new(4)];
        }
        public sealed class IntHolder:Holder<int> {
            public override int[] Array() => [5,1,3,5,2,8,1,6,4];
        }
        public sealed class Probe {
            public static string RunFLinq() => new ItemHolder().RunFLinq()+"#"+new IntHolder().RunFLinq();
            public static string RunLinq() => new ItemHolder().RunLinq()+"#"+new IntHolder().RunLinq();
        }
        """;

    [Fact]
    public void Spanを末尾から回して段をその場に展開する() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"SpanReverseConsumeProbe",8);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(expected.Split('|','#'),actual.Split('|','#'));
        Assert.Contains("reverseSpan",generated);
        Assert.Contains("reverseBuffer",generated);
    }
}
