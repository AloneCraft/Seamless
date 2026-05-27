using System.Linq;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class AsQueryable:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=global::System.Linq.Queryable.AsQueryable(LinqChain1TestBase.Source());
        var actual=LinqChain1TestBase.Source().AsQueryable();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
