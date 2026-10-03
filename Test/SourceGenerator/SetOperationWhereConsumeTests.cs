using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// Where と集合演算(Union/Except/Intersect と By 形)の組み合わせを Consume へ流したとき、結果(順・重複除き)が System.Linq と同じこと。
/// Intersect は第2ソースの集合から取り除けたものだけを流す(集合1つ)。Except は第2ソースの集合に無く、出したものの集合へ足せたものだけを流す。
/// 根と Where の並びは、集合演算と Where の列挙子の構造体を回さずに根を直接回す。
/// </summary>
[LinqIntercept(false)]
public sealed class SetOperationWhereConsumeTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Item(int value):IEquatable<Item> {
            public int Value { get; } = value;
            public bool Equals(Item? other) => other is not null&&other.Value==this.Value;
            public override bool Equals(object? obj) => this.Equals(obj as Item);
            public override int GetHashCode() => this.Value;
            public override string ToString() => this.Value.ToString();
        }
        public sealed class Sink {
            public readonly List<string> Values = new();
            public void Consume<T>(T value) => this.Values.Add(value!.ToString()!);
        }
        public abstract class Holder<T> where T : notnull {
            public abstract T[] First();
            public abstract IEnumerable<T> Second();
            public List<T> FirstList() => new(this.First());
            public static int N(T value) => value is Item item ? item.Value : (int)(object)value;
            public string RunFLinq() {
                var results = new List<string>();
                foreach(var action in new Action<Sink>[] {
                    sink => this.IntersectWhere(sink),sink => this.ExceptWhere(sink),sink => this.UnionWhere(sink),
                    sink => this.WhereIntersectWhere(sink),sink => this.ListExceptWhere(sink),sink => this.IntersectByWhere(sink),
                    sink => this.ExceptByWhere(sink),sink => this.ArrayToHashSet(sink),sink => this.WhereToHashSet(sink),
                }) {
                    var sink = new Sink();
                    action(sink);
                    results.Add(string.Join(",",sink.Values));
                }
                return string.Join("|",results);
            }
            [LinqIntercept(false)]
            public string RunLinq() => string.Join("|",new IEnumerable<object>[] {
                this.First().Intersect(this.Second()).Where(x => N(x)%2==1).Cast<object>(),
                this.First().Except(this.Second()).Where(x => N(x)>1).Cast<object>(),
                this.First().Union(this.Second()).Where(x => N(x)!=4).Cast<object>(),
                this.First().Where(x => N(x)<9).Intersect(this.Second().Where(x => N(x)>1)).Cast<object>(),
                this.FirstList().Except(this.Second()).Where(x => N(x)%3!=0).Cast<object>(),
                this.First().IntersectBy(this.Second().Select(x => N(x)%4),x => N(x)%4).Where(x => N(x)>1).Cast<object>(),
                this.First().ExceptBy(this.Second().Select(x => N(x)%4),x => N(x)%4).Where(x => N(x)>0).Cast<object>(),
                this.First().ToHashSet().Cast<object>(),
                this.Second().Where(x => N(x)>2).ToHashSet().Cast<object>(),
            }.Select(values => string.Join(",",values)));
            [LinqIntercept(true)] private void IntersectWhere(Sink sink) => sink.Consume(this.First().Intersect(this.Second()).Where(x => N(x)%2==1));
            [LinqIntercept(true)] private void ExceptWhere(Sink sink) => sink.Consume(this.First().Except(this.Second()).Where(x => N(x)>1));
            [LinqIntercept(true)] private void UnionWhere(Sink sink) => sink.Consume(this.First().Union(this.Second()).Where(x => N(x)!=4));
            [LinqIntercept(true)] private void WhereIntersectWhere(Sink sink) => sink.Consume(this.First().Where(x => N(x)<9).Intersect(this.Second().Where(x => N(x)>1)));
            [LinqIntercept(true)] private void ListExceptWhere(Sink sink) => sink.Consume(this.FirstList().Except(this.Second()).Where(x => N(x)%3!=0));
            [LinqIntercept(true)] private void IntersectByWhere(Sink sink) => sink.Consume(this.First().IntersectBy(this.Second().Select(x => N(x)%4),x => N(x)%4).Where(x => N(x)>1));
            // ToHashSet().Consume は集合へ足せた(初出の)要素をその場で流すのと同じ(足すだけの集合の列挙順は追加順)。
            [LinqIntercept(true)] private void ArrayToHashSet(Sink sink) => sink.Consume(this.First().ToHashSet());
            [LinqIntercept(true)] private void WhereToHashSet(Sink sink) => sink.Consume(this.Second().Where(x => N(x)>2).ToHashSet());
            [LinqIntercept(true)] private void ExceptByWhere(Sink sink) => sink.Consume(this.First().ExceptBy(this.Second().Select(x => N(x)%4),x => N(x)%4).Where(x => N(x)>0));
        }
        public sealed class ItemHolder:Holder<Item> {
            public override Item[] First() => [new(5),new(1),new(3),new(5),new(2),new(8),new(1),new(6),new(4),new(9),new(3)];
            public override IEnumerable<Item> Second() {
                foreach(var value in new[] { 2,9,2,5,4,7,3,3 }) {
                    yield return new Item(value);
                }
            }
        }
        public sealed class IntHolder:Holder<int> {
            public override int[] First() => [5,1,3,5,2,8,1,6,4,9,3];
            public override IEnumerable<int> Second() {
                foreach(var value in new[] { 2,9,2,5,4,7,3,3 }) {
                    yield return value;
                }
            }
        }
        public sealed class Probe {
            public static string RunFLinq() => new ItemHolder().RunFLinq()+"#"+new IntHolder().RunFLinq();
            public static string RunLinq() => new ItemHolder().RunLinq()+"#"+new IntHolder().RunLinq();
        }
        """;

    [Fact]
    public void 集合演算とWhereの組み合わせがSystemLinqと同じ() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"SetOperationWhereConsumeProbe",9);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(expected.Split('|','#'),actual.Split('|','#'));
        // Intersect は第2ソースの集合から取り除けたものだけを流す(集合1つ)。
        Assert.Contains(".Remove(__it",generated);
        Assert.Contains("__hashSetRoot",generated);
    }
}
