using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>
/// GroupJoin の結果選択子が内側のグループを使わず、キー選択子が恒等で比較子も既定のときに、探索表を作らない形の意味を確かめる。
/// FLinq の契約(.Anchor の無い式は副作用・例外なしとみなす)どおり、.Anchor の無い内側は列挙もしない。
/// 内側のソース式に .Anchor を付けたときだけ、内側を1回列挙して副作用と例外を残す。内側のグループを使う形は従来どおり探索表を作る。
/// </summary>
[LinqIntercept(false)]
public sealed class GroupJoinLookupSkipTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        public sealed class Consumer<T> {
            public readonly List<T> Items = new();
            public void Consume(T item) => this.Items.Add(item);
        }
        public static class ConsumeSupport {
            public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                foreach(var item in source) {
                    consumer.Consume(item);
                }
            }
        }
        public sealed class Container {
            public static int InnerMoves;
            [LinqIntercept(false)]
            private static IEnumerable<int> Counted(int[] values) {
                foreach(var value in values) {
                    InnerMoves++;
                    yield return value;
                }
            }
            [LinqIntercept(false)]
            private static IEnumerable<int> Throwing() {
                yield return 1;
                throw new InvalidOperationException("inner");
            }
            public static void RunArrayInner(int[] outer,int[] inner,Consumer<int> consumer) => ArrayInner(outer,inner,consumer);
            public static void RunListInner(int[] outer,int[] inner,Consumer<int> consumer) => ListInner(outer,inner,consumer);
            public static void RunEnumerableInner(int[] outer,int[] inner,Consumer<int> consumer) => EnumerableInner(outer,inner,consumer);
            public static void RunThrowingInner(int[] outer,int[] inner,Consumer<int> consumer) => ThrowingInner(outer,consumer);
            public static void RunEnumerableAnchored(int[] outer,int[] inner,Consumer<int> consumer) => EnumerableAnchored(outer,inner,consumer);
            public static void RunThrowingAnchored(int[] outer,int[] inner,Consumer<int> consumer) => ThrowingAnchored(outer,consumer);
            public static void RunUsesGroup(int[] outer,int[] inner,Consumer<int> consumer) => UsesGroup(outer,inner,consumer);
            public static void RunToArray(int[] outer,int[] inner,Consumer<int> consumer) {
                foreach(var value in ToArray(outer,inner)) {
                    consumer.Consume(value);
                }
            }
            [LinqIntercept(true)]
            private static void ArrayInner(int[] outer,int[] inner,Consumer<int> consumer) =>
                outer.GroupJoin(inner,x => x,x => x,(o,g) => o).Consume(consumer);
            [LinqIntercept(true)]
            private static void ListInner(int[] outer,int[] inner,Consumer<int> consumer) =>
                outer.GroupJoin(new List<int>(inner),x => x,x => x,(o,g) => o).Consume(consumer);
            [LinqIntercept(true)]
            private static void EnumerableInner(int[] outer,int[] inner,Consumer<int> consumer) =>
                outer.GroupJoin(Counted(inner),x => x,x => x,(o,g) => o).Consume(consumer);
            [LinqIntercept(true)]
            private static void ThrowingInner(int[] outer,Consumer<int> consumer) =>
                outer.GroupJoin(Throwing(),x => x,x => x,(o,g) => o).Consume(consumer);
            [LinqIntercept(true)]
            private static void EnumerableAnchored(int[] outer,int[] inner,Consumer<int> consumer) =>
                outer.GroupJoin(Counted(inner).Anchor,x => x,x => x,(o,g) => o).Consume(consumer);
            [LinqIntercept(true)]
            private static void ThrowingAnchored(int[] outer,Consumer<int> consumer) =>
                outer.GroupJoin(Throwing().Anchor,x => x,x => x,(o,g) => o).Consume(consumer);
            [LinqIntercept(true)]
            private static void UsesGroup(int[] outer,int[] inner,Consumer<int> consumer) =>
                outer.GroupJoin(inner,x => x,x => x,(o,g) => o*100+g.Count()).Consume(consumer);
            [LinqIntercept(true)]
            private static int[] ToArray(int[] outer,int[] inner) =>
                outer.GroupJoin(inner,x => x,x => x,(o,g) => o).ToArray();
        }
        """;

    private static readonly int[] Outer = [3,1,2,3];
    private static readonly int[] Inner = [2,3,3,5];

    [Theory]
    [InlineData("ArrayInner","3,1,2,3")]
    [InlineData("ListInner","3,1,2,3")]
    [InlineData("EnumerableInner","3,1,2,3")]
    [InlineData("EnumerableAnchored","3,1,2,3")]
    [InlineData("ThrowingInner","3,1,2,3")]
    [InlineData("ToArray","3,1,2,3")]
    [InlineData("UsesGroup","302,100,201,302")]
    public void System_Linqと同じ結果になる(string method,string expected) {
        var (actual,_,_) = Run(method);
        Assert.Equal(expected,actual);
    }

    [Fact]
    public void Anchorの無い内側は列挙しない() {
        {
            //副作用は起きない(純粋とみなして列挙を省く)
            var (_,innerMoves,_) = Run("EnumerableInner");
            Assert.Equal(0,innerMoves);
        }
        {
            //列挙で起きるはずの例外も起きない
            var (_,_,exception) = Run("ThrowingInner");
            Assert.Null(exception);
        }
    }

    [Fact]
    public void Anchorを付けた内側は1回だけ列挙し例外も残る() {
        {
            var (_,innerMoves,_) = Run("EnumerableAnchored");
            Assert.Equal(Inner.Length,innerMoves);
        }
        {
            var (_,_,exception) = Run("ThrowingAnchored");
            Assert.Equal("inner",Assert.IsType<InvalidOperationException>(exception).Message);
        }
    }

    [Fact]
    public void 内側のグループを使わない形だけ探索表を作らない() {
        var (_,generated) = ExecutableGeneration.Generate(Source,"GroupJoinLookupSkipShape",8);
        {
            //内側のグループを使わない7つの形は探索表を作らない
            Assert.Equal(7,generated.Split("DrainInner(this.").Length-1);
        }
        {
            //内側のグループを使う形は従来どおり探索表を作る
            Assert.Equal(1,generated.Split("=BuildLookup(this.").Length-1);
        }
        {
            //内側を列挙するのは .Anchor を付けた2つだけ
            Assert.Equal(2,global::System.Text.RegularExpressions.Regex.Matches(generated,@"while\(__ie\d+\.TryGetNext\(out _\)\)").Count);
        }
    }

    private static (string Result,int InnerMoves,Exception? Exception) Run(string method) {
        var (compilation,_) = ExecutableGeneration.Generate(Source,"GroupJoinLookupSkipProbe",8);
        return ExecutableGeneration.Run(compilation,"Sample.Container",container => {
            var probe = container.GetMethod("Run"+method)!;
            var consumerType = probe.GetParameters()[2].ParameterType;
            var consumer = Activator.CreateInstance(consumerType)!;
            Exception? exception = null;
            try {
                probe.Invoke(null,[Outer,Inner,consumer]);
            } catch(global::System.Reflection.TargetInvocationException invocation) {
                exception=invocation.InnerException;
            }
            var items = (global::System.Collections.IEnumerable)consumerType.GetField("Items")!.GetValue(consumer)!;
            var moves = (int)container.GetField("InnerMoves")!.GetValue(null)!;
            return (string.Join(",",items.Cast<object>()),moves,exception);
        });
    }
}
