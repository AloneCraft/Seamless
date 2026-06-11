using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class Concat_Select:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Concat(Source(),Second()),static v => v+100).ToList();
        var actual=new Consumer<int>();
        Source().Concat(Second()).Select(static v => v+100).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
