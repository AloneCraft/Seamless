using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Chunk:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void size() => sizeImpl();

    private static void sizeImpl() {
        var expected=SystemEnumerable.Chunk(LinqChain1TestBase.Source(),2).ToList();
        var actual=LinqChain1TestBase.Source().Chunk(2).ToList();
        LinqChain1TestBase.AssertNestedSequenceEqual(expected,actual);
    }

}
