using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Append:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element() => elementImpl();

    private static void elementImpl() {
        var expected=SystemEnumerable.Append(LinqChain1TestBase.Source(),5);
        var actual=LinqChain1TestBase.Source().Append(5);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
