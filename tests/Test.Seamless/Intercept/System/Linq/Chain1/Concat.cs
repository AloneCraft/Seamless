using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Concat:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Concat(Source(),Second()).ToList();
        var actual=new Consumer<int>();
        Source().Concat(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
