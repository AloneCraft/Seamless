using System.Linq;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class AsQueryable:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=Queryable.AsQueryable(Source());
        var actual=Source().AsQueryable();
        AssertSequenceEqual(expected,actual);
    }

}
