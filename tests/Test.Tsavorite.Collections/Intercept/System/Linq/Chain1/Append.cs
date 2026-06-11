using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Append:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void element() => elementImpl();

    private static void elementImpl() {
        var expected=SystemEnumerable.Append(Source(),5).ToList();
        var actual=new Consumer<int>();
        Source().Append(5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
