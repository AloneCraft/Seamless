using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// GroupBy・ToLookup・Join の探索表を ContiguousLookupBuilder(キーへ初出順のグループ番号を振ってから要素を1本の配列へ並べる)で作っても、
/// 結果(グループの並び・グループ内の要素の並び・null キー・比較子・内側の重複)が System.Linq と同じであること。
/// 参照型の要素(共有ジェネリックコード)と int の両方、配列(直接 Consume の経路)と IEnumerable(列挙子の構造体の経路)の両方で試す。
/// </summary>
[LinqIntercept(false)]
public sealed class ContiguousLookupGenerationTests {
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
            // 結果選択子がグループを IEnumerable<T> として返すとき、System.Linq は要素の配列を、FLinq はグループを渡す(どちらも正しい)ので、要素だけを比べる。
            public void Consume<T>(T value) => this.Values.Add(typeof(T).IsGenericType&&typeof(T).GetGenericTypeDefinition()==typeof(IEnumerable<>) ? Elements(value) : Format(value));
            public static string Elements(object? value) {
                var parts = new List<string>();
                foreach(var element in (System.Collections.IEnumerable)value!) {
                    parts.Add(Format(element));
                }
                return "["+string.Join(" ",parts)+"]";
            }
            public static string Format(object? value) {
                if(value is null) {
                    return "null";
                }
                var keyProperty = value.GetType().GetProperty("Key");
                if(keyProperty is not null&&value is System.Collections.IEnumerable elements) {
                    var parts = new List<string>();
                    foreach(var element in elements) {
                        parts.Add(Format(element));
                    }
                    return (keyProperty.GetValue(value)?.ToString()??"null")+":"+string.Join(" ",parts);
                }
                return value.ToString()!;
            }
        }
        public abstract class Holder<T> where T : notnull {
            public abstract T[] First();
            public abstract IEnumerable<T> Second();
            public static int N(T value) => value is Item item ? item.Value : (int)(object)value;
            public static string? S(T value) => N(value)%5==0 ? null : (N(value)%3==0 ? "A" : "b");
            public string RunFLinq() {
                var results = new List<string>();
                foreach(var action in new Action<Sink>[] {
                    sink => this.GroupByKey(sink),sink => this.GroupByElement(sink),sink => this.GroupByResult(sink),sink => this.GroupByNullKey(sink),
                    sink => this.GroupByEnumerable(sink),sink => this.ToLookupKey(sink),sink => this.ToLookupElement(sink),sink => this.ToLookupEnumerable(sink),
                    sink => sink.Values.Add(this.ToLookupIndex()),sink => sink.Values.Add(this.ToLookupComparer()),sink => this.JoinArrays(sink),sink => this.JoinEnumerable(sink),
                    sink => this.JoinNullKeys(sink),sink => this.JoinWhere(sink),sink => this.JoinEmpty(sink),
                    sink => this.JoinOuterOnlyArrays(sink),sink => this.JoinOuterOnlyEnumerable(sink),sink => this.JoinOuterOnlyNullKeys(sink),
                }) {
                    var sink = new Sink();
                    action(sink);
                    results.Add(string.Join(",",sink.Values));
                }
                return string.Join("|",results);
            }
            [LinqIntercept(false)]
            public string RunLinq() {
                var lookup = this.First().ToLookup(x => N(x)%3);
                var comparerLookup = this.First().ToLookup(x => S(x),global::System.StringComparer.OrdinalIgnoreCase);
                return string.Join("|",new IEnumerable<object?>[] {
                    this.First().GroupBy(x => N(x)%3).Cast<object?>(),
                    this.First().GroupBy(x => N(x)%3,x => N(x)*2).Cast<object?>(),
                    this.First().GroupBy(x => N(x)%3,(key,group) => group).Select(group => (object?)Sink.Elements(group)),
                    this.First().GroupBy(x => S(x)).Cast<object?>(),
                    this.Second().GroupBy(x => N(x)%2).Cast<object?>(),
                    this.First().ToLookup(x => N(x)%3).Cast<object?>(),
                    this.First().ToLookup(x => S(x),x => N(x)+1).Cast<object?>(),
                    this.Second().ToLookup(x => N(x)%2).Cast<object?>(),
                    new object?[] { string.Join(" ",lookup[1].Select(x => N(x)))+"/"+lookup.Count+"/"+lookup.Contains(7)+"/"+lookup[7].Count() },
                    new object?[] { string.Join(" ",comparerLookup["a"].Select(x => N(x)))+"/"+comparerLookup.Count+"/"+string.Join(" ",comparerLookup[null].Select(x => N(x))) },
                    this.First().Join(this.First(),x => N(x)%4,y => N(y)%4,(a,b) => N(a)*100+N(b)).Cast<object?>(),
                    this.Second().Join(this.First(),x => N(x)%3,y => N(y)%3,(a,b) => N(a)*100+N(b)).Cast<object?>(),
                    this.First().Join(this.Second(),x => S(x),y => S(y),(a,b) => N(a)*100+N(b)).Cast<object?>(),
                    this.First().Join(this.Second(),x => N(x)%3,y => N(y)%3,(a,b) => N(a)*100+N(b)).Where(x => x%2==0).Cast<object?>(),
                    this.First().Join(Array.Empty<T>(),x => N(x),y => N(y),(a,b) => N(a)).Cast<object?>(),
                    this.First().Join(this.First(),x => N(x)%4,y => N(y)%4,(a,b) => N(a)).Cast<object?>(),
                    this.Second().Join(this.Second(),x => N(x)%3,y => N(y)%3,(a,b) => a).Cast<object?>(),
                    this.First().Join(this.Second(),x => S(x),y => S(y),(a,b) => N(a)*2).Cast<object?>(),
                }.Select(values => string.Join(",",values.Select(Sink.Format))));
            }
            [LinqIntercept(true)] private void GroupByKey(Sink sink) => sink.Consume(this.First().GroupBy(x => N(x)%3));
            [LinqIntercept(true)] private void GroupByElement(Sink sink) => sink.Consume(this.First().GroupBy(x => N(x)%3,x => N(x)*2));
            [LinqIntercept(true)] private void GroupByResult(Sink sink) => sink.Consume(this.First().GroupBy(x => N(x)%3,(key,group) => group));
            [LinqIntercept(true)] private void GroupByNullKey(Sink sink) => sink.Consume(this.First().GroupBy(x => S(x)));
            [LinqIntercept(true)] private void GroupByEnumerable(Sink sink) => sink.Consume(this.Second().GroupBy(x => N(x)%2));
            [LinqIntercept(true)] private void ToLookupKey(Sink sink) => sink.Consume(this.First().ToLookup(x => N(x)%3));
            [LinqIntercept(true)] private void ToLookupElement(Sink sink) => sink.Consume(this.First().ToLookup(x => S(x),x => N(x)+1));
            [LinqIntercept(true)] private void ToLookupEnumerable(Sink sink) => sink.Consume(this.Second().ToLookup(x => N(x)%2));
            // 文字列を sink.Consume へ渡すと文字の列として流す形に読まれるので、結果の文字列は返り値で受ける。
            // 比較子の式は生成コードへそのまま写される(既存の不具合: using の無い型名は解決できない)ので完全修飾で書く。
            [LinqIntercept(true)]
            private string ToLookupIndex() {
                var lookup = this.First().ToLookup(x => N(x)%3);
                return Holder<T>.Join(lookup[1])+"/"+lookup.Count+"/"+lookup.Contains(7)+"/"+Holder<T>.Count(lookup[7]);
            }
            [LinqIntercept(true)]
            private string ToLookupComparer() {
                var lookup = this.First().ToLookup(x => S(x),global::System.StringComparer.OrdinalIgnoreCase);
                return Holder<T>.Join(lookup["a"])+"/"+lookup.Count+"/"+Holder<T>.Join(lookup[null]);
            }
            [LinqIntercept(true)] private void JoinArrays(Sink sink) => sink.Consume(this.First().Join(this.First(),x => N(x)%4,y => N(y)%4,(a,b) => N(a)*100+N(b)));
            [LinqIntercept(true)] private void JoinEnumerable(Sink sink) => sink.Consume(this.Second().Join(this.First(),x => N(x)%3,y => N(y)%3,(a,b) => N(a)*100+N(b)));
            [LinqIntercept(true)] private void JoinNullKeys(Sink sink) => sink.Consume(this.First().Join(this.Second(),x => S(x),y => S(y),(a,b) => N(a)*100+N(b)));
            [LinqIntercept(true)] private void JoinWhere(Sink sink) => sink.Consume(this.First().Join(this.Second(),x => N(x)%3,y => N(y)%3,(a,b) => N(a)*100+N(b)).Where(x => x%2==0));
            // 結果選択子が内側の要素を使わない形(キーごとの件数だけを数える)。
            [LinqIntercept(true)] private void JoinOuterOnlyArrays(Sink sink) => sink.Consume(this.First().Join(this.First(),x => N(x)%4,y => N(y)%4,(a,b) => N(a)));
            [LinqIntercept(true)] private void JoinOuterOnlyEnumerable(Sink sink) => sink.Consume(this.Second().Join(this.Second(),x => N(x)%3,y => N(y)%3,(a,b) => a));
            [LinqIntercept(true)] private void JoinOuterOnlyNullKeys(Sink sink) => sink.Consume(this.First().Join(this.Second(),x => S(x),y => S(y),(a,b) => N(a)*2));
            [LinqIntercept(true)] private void JoinEmpty(Sink sink) => sink.Consume(this.First().Join(Array.Empty<T>(),x => N(x),y => N(y),(a,b) => N(a)));
            [LinqIntercept(false)]
            public static string Join(IEnumerable<T> values) {
                var parts = new List<string>();
                foreach(var value in values) {
                    parts.Add(N(value).ToString());
                }
                return string.Join(" ",parts);
            }
            [LinqIntercept(false)]
            public static int Count(IEnumerable<T> values) {
                var count = 0;
                foreach(var _ in values) {
                    count++;
                }
                return count;
            }
        }
        public sealed class ItemHolder:Holder<Item> {
            public override Item[] First() => [new(5),new(1),new(3),new(5),new(2),new(8),new(1),new(6),new(4),new(10),new(9)];
            public override IEnumerable<Item> Second() {
                foreach(var value in new[] { 2,9,2,5,4,7,15 }) {
                    yield return new Item(value);
                }
            }
        }
        public sealed class IntHolder:Holder<int> {
            public override int[] First() => [5,1,3,5,2,8,1,6,4,10,9];
            public override IEnumerable<int> Second() {
                foreach(var value in new[] { 2,9,2,5,4,7,15 }) {
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
    public void 探索表を1本の配列で作っても結果はSystemLinqと同じ() {
        var (compilation,generated) = ExecutableGeneration.Generate(Source,"ContiguousLookupProbe",18);
        var (actual,expected) = ExecutableGeneration.Run(compilation,"Sample.Probe",probe => (
            (string)probe.GetMethod("RunFLinq")!.Invoke(null,null)!,
            (string)probe.GetMethod("RunLinq")!.Invoke(null,null)!));
        Assert.Equal(expected.Split('|','#'),actual.Split('|','#'));
        // GroupBy・ToLookup・Join はグループごとに伸びる配列を持つ LookupBuilder を使わない。
        Assert.Contains("ContiguousLookupBuilder<",generated);
        Assert.DoesNotContain("LookupBuilder<",generated.Replace("ContiguousLookupBuilder<",""));
        // 結果選択子が内側の要素を使わない Join は、キーごとの件数だけを数える(配列の直接 Consume と列挙子の構造体の両方)。
        Assert.Contains("__joinCounts",generated);
        Assert.Contains("BuildCounts(",generated);
    }
}
