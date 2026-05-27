using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Except:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Except(LinqChain1TestBase.Source(),LinqChain1TestBase.Second());
        var actual=LinqChain1TestBase.Source().Except(LinqChain1TestBase.Second());
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_comparer() => second_comparerImpl();

    private static void second_comparerImpl() {
        var expected=SystemEnumerable.Except(LinqChain1TestBase.Source(),LinqChain1TestBase.Second(),EqualityComparer<int>.Default);
        var actual=LinqChain1TestBase.Source().Except(LinqChain1TestBase.Second(),EqualityComparer<int>.Default);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
