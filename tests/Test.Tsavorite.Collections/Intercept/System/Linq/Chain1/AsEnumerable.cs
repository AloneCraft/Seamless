using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class AsEnumerable:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.AsEnumerable(Source()).ToList();
        var actual=new Consumer<int>();
        Source().AsEnumerable().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
