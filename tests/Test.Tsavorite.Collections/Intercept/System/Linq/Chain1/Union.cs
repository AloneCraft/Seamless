using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Union:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Union(Source(),Second()).ToList();
        var actual=new Consumer<int>();
        Source().Union(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() => second_comparerImpl();

    private static void second_comparerImpl() {
        var expected=SystemEnumerable.Union(Source(),Second(),EqualityComparer<int>.Default).ToList();
        var actual=new Consumer<int>();
        Source().Union(Second(),EqualityComparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
