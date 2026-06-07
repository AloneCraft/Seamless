using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Prepend:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element() => elementImpl();

    private static void elementImpl() {
        var expected=SystemEnumerable.Prepend(LinqChain1TestBase.Source(),0).ToList();
        var actual=new Consumer<int>();
        LinqChain1TestBase.Source().Prepend(0).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

}
