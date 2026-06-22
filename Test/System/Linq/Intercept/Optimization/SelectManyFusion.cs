using System.Linq;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class SelectManyFusion:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void NestedFlatten() {
        AssertSequenceEqual(Outer().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList(),NestedFlatten_NonEmptyOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList(),NestedFlatten_NonEmptyNoOptimized());
        AssertSequenceEqual(EmptyOuter().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList(),NestedFlatten_EmptyOptimized());
        AssertSequenceEqual(EmptyOuter().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList(),NestedFlatten_EmptyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> NestedFlatten_NonEmptyOptimized() =>
        Outer().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> NestedFlatten_NonEmptyNoOptimized() =>
        Outer().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> NestedFlatten_EmptyOptimized() =>
        EmptyOuter().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> NestedFlatten_EmptyNoOptimized() =>
        EmptyOuter().SelectMany(static a => Inner().SelectMany(b => new[] { a+b,a-b })).ToList();

    [Fact,Trait("Category","Unit")]
    public static void CollectionSelectorResultSelectorFused() {
        AssertSequenceEqual(Outer().SelectMany(static a => Inner(),static (a,b) => a+b).ToList(),CollectionSelectorResultSelectorFusedOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner(),static (a,b) => a+b).ToList(),CollectionSelectorResultSelectorFusedNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CollectionSelectorResultSelectorFusedOptimized() =>
        Outer().SelectMany(static a => Inner(),static (a,b) => a+b).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CollectionSelectorResultSelectorFusedNoOptimized() =>
        Outer().SelectMany(static a => Inner(),static (a,b) => a+b).ToList();

    [Fact,Trait("Category","Unit")]
    public static void PredicatePushdown() {
        AssertSequenceEqual(Outer().SelectMany(static a => Inner()).Where(static x => x>Limit()).ToList(),PredicatePushdown_MatchOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner()).Where(static x => x>Limit()).ToList(),PredicatePushdown_MatchNoOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner()).Where(static x => x>1000).ToList(),PredicatePushdown_NoMatchOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner()).Where(static x => x>1000).ToList(),PredicatePushdown_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> PredicatePushdown_MatchOptimized() =>
        Outer().SelectMany(static a => Inner()).Where(static x => x>Limit()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> PredicatePushdown_MatchNoOptimized() =>
        Outer().SelectMany(static a => Inner()).Where(static x => x>Limit()).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> PredicatePushdown_NoMatchOptimized() =>
        Outer().SelectMany(static a => Inner()).Where(static x => x>1000).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> PredicatePushdown_NoMatchNoOptimized() =>
        Outer().SelectMany(static a => Inner()).Where(static x => x>1000).ToList();

    [Fact,Trait("Category","Unit")]
    public static void IndexSelectorOverload() {
        AssertSequenceEqual(Outer().SelectMany(static (a,i) => new[] { a,i }).ToList(),IndexSelectorOverloadOptimized());
        AssertSequenceEqual(Outer().SelectMany(static (a,i) => new[] { a,i }).ToList(),IndexSelectorOverloadNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IndexSelectorOverloadOptimized() =>
        Outer().SelectMany(static (a,i) => new[] { a,i }).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IndexSelectorOverloadNoOptimized() =>
        Outer().SelectMany(static (a,i) => new[] { a,i }).ToList();
}
