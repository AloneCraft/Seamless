using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Take:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void count() => countImpl();

    private static void countImpl() {
        var expected=SystemEnumerable.Take(Source(),3).ToList();
        var actual=new Consumer<int>();
        Source().Take(3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void range() => rangeImpl();

    private static void rangeImpl() {
        var expected=SystemEnumerable.Take(Source(),new global::System.Range(1,3)).ToList();
        var actual=Source().Take(new global::System.Range(1,3)).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
