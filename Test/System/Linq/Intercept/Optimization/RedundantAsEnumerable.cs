using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Optimization;

// §1: 冗長な AsEnumerable() の排除。後続に別の Enumerable 拡張がある中段でのみ除去（最外段は素通しのため残す）。
// AsEnumerable は元々ゼロコストの素通しなので、いずれの場合も結果は不変。
public sealed class RedundantAsEnumerable:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void LeadingAsEnumerableRemoved() {
                // src.AsEnumerable().Where(p) -> src.Where(p)。
        var expected=Numbers().AsEnumerable().Where(static x => x>3).ToList();
        LeadingAsEnumerableRemovedOptimized(expected);
        LeadingAsEnumerableRemovedNoOptimized(expected);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LeadingAsEnumerableRemovedOptimized(dynamic expected) {
                // src.AsEnumerable().Where(p) -> src.Where(p)。
        var actual=new Consumer<int>();
                Numbers().AsEnumerable().Where(static x => x>3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void LeadingAsEnumerableRemovedNoOptimized(dynamic expected) {
                // src.AsEnumerable().Where(p) -> src.Where(p)。
        var actual=new Consumer<int>();
                Numbers().AsEnumerable().Where(static x => x>3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void MiddleAsEnumerableRemoved() {
                // src.Where(p).AsEnumerable().Select(s) -> src.Where(p).Select(s)。
        var expected=Numbers().Where(static x => x>3).AsEnumerable().Select(static x => x*2).ToList();
        MiddleAsEnumerableRemovedOptimized(expected);
        MiddleAsEnumerableRemovedNoOptimized(expected);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void MiddleAsEnumerableRemovedOptimized(dynamic expected) {
                // src.Where(p).AsEnumerable().Select(s) -> src.Where(p).Select(s)。
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>3).AsEnumerable().Select(static x => x*2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void MiddleAsEnumerableRemovedNoOptimized(dynamic expected) {
                // src.Where(p).AsEnumerable().Select(s) -> src.Where(p).Select(s)。
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>3).AsEnumerable().Select(static x => x*2).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void OutermostAsEnumerableKept() {
                // src.Where(p).AsEnumerable() -> 最外段の AsEnumerable は残るが結果は不変。
        var expected=Numbers().Where(static x => x>3).AsEnumerable().ToList();
        OutermostAsEnumerableKeptOptimized(expected);
        OutermostAsEnumerableKeptNoOptimized(expected);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void OutermostAsEnumerableKeptOptimized(dynamic expected) {
                // src.Where(p).AsEnumerable() -> 最外段の AsEnumerable は残るが結果は不変。
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>3).AsEnumerable().Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void OutermostAsEnumerableKeptNoOptimized(dynamic expected) {
                // src.Where(p).AsEnumerable() -> 最外段の AsEnumerable は残るが結果は不変。
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>3).AsEnumerable().Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
