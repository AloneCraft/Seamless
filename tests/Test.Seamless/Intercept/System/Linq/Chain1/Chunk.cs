using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Chunk:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void size() => sizeImpl();

    private static void sizeImpl() {
        var expected=SystemEnumerable.Chunk(Source(),2).ToList();
        var actual=new Consumer<int[]>();
        Source().Chunk(2).Consume(actual);
        AssertNestedSequenceEqual(expected,actual.Items);
    }

}
