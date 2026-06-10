using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class Cast:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Cast<int>(CastObjects()).ToList();
        var actual=new Consumer<int>();
        CastObjects().Cast<int>().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
