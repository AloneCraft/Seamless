using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class Select_Take:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Take(SystemEnumerable.Select(Source(),static v => v*2),3).ToList();
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Take(3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
