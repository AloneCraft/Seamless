using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Intersect:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Intersect(LinqChain1TestBase.Source(),LinqChain1TestBase.Second()).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Intersect(LinqChain1TestBase.Second()).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() => second_comparerImpl();

    private static void second_comparerImpl() {
        var expected=SystemEnumerable.Intersect(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),EqualityComparer<int>.Default).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Intersect(LinqChain1TestBase.Second(),EqualityComparer<int>.Default).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

}
