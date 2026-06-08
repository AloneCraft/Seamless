using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Repeat:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element_count() => element_countImpl();

    private static void element_countImpl() {
        var expected=SystemEnumerable.Repeat(7,3).ToList();
        var actual=Enumerable.Repeat(7,3).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
