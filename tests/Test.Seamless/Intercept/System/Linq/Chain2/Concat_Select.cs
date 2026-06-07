using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Concat_Select:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Concat(LinqChain2TestBase.Source(),LinqChain2TestBase.Second()),static v => v+100).ToList();
        var actual=new Consumer<int>();
        LinqChain2TestBase.Source().Concat(LinqChain2TestBase.Second()).Select(static v => v+100).Consume(actual);
        LinqChain2TestBase.AssertSequenceEqual(expected,actual.Items);
    }
}
