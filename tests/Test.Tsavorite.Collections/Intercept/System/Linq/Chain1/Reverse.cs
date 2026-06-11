using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Reverse:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Reverse(Source()).ToList();
        var actual=new Consumer<int>();
        Source().Reverse().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
