using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// 段をまとめた列(状態を持つ段を含む)を Consume へ流すとき、列挙子の構造体を作らず、フィールドを局所変数・TryGetNext を局所関数にして
/// 差し替えメソッドの中に展開すること(参照型の要素の共有ジェネリックコードで入れ子の呼び出しがインライン化されないため)。
/// 結果は System.Linq と同じ。
/// </summary>
[LinqIntercept(false)]
public sealed class FlattenedPipelineConsumeTests {
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
            public static int N(T value) => value is Item item ? item.Value : (int)(object)value;
            public string RunFLinq() {
                var results = new List<string>();
                foreach(var action in new Action<Sink>[] {
                    sink => this.DistinctWhere(sink),sink => this.WhereConcat(sink),sink => this.UnionWhere(sink),sink => this.ExceptSelect(sink),
                    sink => this.IntersectWhere(sink),sink => this.ReverseWhere(sink),sink => this.SkipTakeSelect(sink),sink => this.OrderWhere(sink),
                    sink => this.SelectDistinct(sink),sink => this.EnumerableDistinctWhere(sink),
                }) {
                    var sink = new Sink();
                    action(sink);
                    results.Add(string.Join(",",sink.Values));
                }
                return string.Join("|",results);
            }
            [LinqIntercept(false)]
            public string RunLinq() => string.Join("|",new IEnumerable<object>[] {
                this.First().Distinct().Where(x => N(x)%2==1).Cast<object>(),
                this.First().Where(x => N(x)>2).Concat(this.Second()).Cast<object>(),
                this.First().Union(this.Second()).Where(x => N(x)!=4).Cast<object>(),
                this.First().Except(this.Second()).Select(x => N(x)*10).Cast<object>(),
                this.First().Intersect(this.Second()).Where(x => N(x)>0).Cast<object>(),
                this.First().Reverse().Where(x => N(x)%3!=0).Cast<object>(),
                this.First().Skip(2).Take(4).Select(x => N(x)+100).Cast<object>(),
                this.First().OrderBy(x => N(x)%3).Where(x => N(x)<7).Cast<object>(),
                this.First().Select(x => N(x)%4).Distinct().Cast<object>(),
                this.Second().Distinct().Where(x => N(x)>1).Cast<object>(),
            }.Select(values => string.Join(",",values)));
            [LinqIntercept(true)] private void DistinctWhere(Sink sink) => sink.Consume(this.First().Distinct().Where(x => N(x)%2==1));
            [LinqIntercept(true)] private void WhereConcat(Sink sink) => sink.Consume(this.First().Where(x => N(x)>2).Concat(this.Second()));
            [LinqIntercept(true)] private void UnionWhere(Sink sink) => sink.Consume(this.First().Union(this.Second()).Where(x => N(x)!=4));
            [LinqIntercept(true)] private void ExceptSelect(Sink sink) => sink.Consume(this.First().Except(this.Second()).Select(x => N(x)*10));
            [LinqIntercept(true)] private void IntersectWhere(Sink sink) => sink.Consume(this.First().Intersect(this.Second()).Where(x => N(x)>0));
            [LinqIntercept(true)] private void ReverseWhere(Sink sink) => sink.Consume(this.First().Reverse().Where(x => N(x)%3!=0));
            [LinqIntercept(true)] private void SkipTakeSelect(Sink sink) => sink.Consume(this.First().Skip(2).Take(4).Select(x => N(x)+100));
            [LinqIntercept(true)] private void OrderWhere(Sink sink) => sink.Consume(this.First().OrderBy(x => N(x)%3).Where(x => N(x)<7));
            [LinqIntercept(true)] private void SelectDistinct(Sink sink) => sink.Consume(this.First().Select(x => N(x)%4).Distinct());
            [LinqIntercept(true)] private void EnumerableDistinctWhere(Sink sink) => sink.Consume(this.Second().Distinct().Where(x => N(x)>1));
        }
        public sealed class ItemHolder:Holder<Item> {
            public override Item[] First() => [new(5),new(1),new(3),new(5),new(2),new(8),new(1),new(6),new(4)];
            public override IEnumerable<Item> Second() {
                foreach(var value in new[] { 2,9,2,5,4,7 }) {
                    yield return new Item(value);
                }
            }
        }
        public sealed class Probe {
            public static string RunFLinq() => new ItemHolder().RunFLinq();
            public static string RunLinq() => new ItemHolder().RunLinq();
        }
        """;

    [Fact]
    public void 段をまとめた列を差し替えメソッドの中に展開する() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"FlattenedPipelineConsumeProbe",10);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(expected.Split('|'),actual.Split('|'));
        // 汎用の入れ子の Consume(列挙子の構造体を作って TryGetNext で回す形)は、並べ替え(比較子の型をフィールドに持つ)の1つだけ残る。
        Assert.Single(global::System.Text.RegularExpressions.Regex.Matches(generated,@"while\(ce"));
    }
}
