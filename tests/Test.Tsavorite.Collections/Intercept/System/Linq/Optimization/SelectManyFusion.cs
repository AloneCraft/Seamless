using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// ルール §2: SelectMany の平坦化・融合と Where の述語プッシュダウン。
public sealed class SelectManyFusion:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void NestedFlatten() => NestedFlattenImpl();

    private static void NestedFlattenImpl(){
        {
            // 入れ子 SelectMany を平坦なチェーンへ。
            var expected=SystemEnumerable.SelectMany(Outer(),
                static a => SystemEnumerable.SelectMany(Inner(),b => new[] { a+b,a-b })).ToList();
            var actual=new Consumer<int>();
            Outer().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
        {
            var expected=SystemEnumerable.SelectMany(EmptyOuter(),
                static a => SystemEnumerable.SelectMany(Inner(),b => new[] { a+b,a-b })).ToList();
            var actual=new Consumer<int>();
            EmptyOuter().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void CollectionSelectorResultSelectorFused() => CollectionSelectorResultSelectorFusedImpl();

    private static void CollectionSelectorResultSelectorFusedImpl(){
        // collectionSelector+resultSelector の融合オーバーロード（中間 iterator 割当を消す）。
        var expected=SystemEnumerable.SelectMany(Outer(),static a => Inner(),static (a,b) => a+b).ToList();
        var actual=new Consumer<int>();
        Outer().SelectMany(static a => Inner(),static (a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void PredicatePushdown() => PredicatePushdownImpl();

    private static void PredicatePushdownImpl(){
        {
            // 平坦化後の Where は内側へ押し下げられる。結果は不変。
            var expected=SystemEnumerable.Where(
                SystemEnumerable.SelectMany(Outer(),static a => Inner()),static x => x>Limit()).ToList();
            var actual=new Consumer<int>();
            Outer().SelectMany(static a => Inner()).Where(static x => x>Limit()).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
        {
            // 全要素が脱落するケース。
            var expected=SystemEnumerable.Where(
                SystemEnumerable.SelectMany(Outer(),static a => Inner()),static x => x>1000).ToList();
            var actual=new Consumer<int>();
            Outer().SelectMany(static a => Inner()).Where(static x => x>1000).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void IndexSelectorOverload() => IndexSelectorOverloadImpl();

    private static void IndexSelectorOverloadImpl(){
        var expected=SystemEnumerable.SelectMany(Outer(),static (a,i) => new[] { a,i }).ToList();
        var actual=new Consumer<int>();
        Outer().SelectMany(static (a,i) => new[] { a,i }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
