using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Skip_Take:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Take(SystemEnumerable.Skip(LinqChain2TestBase.Source(),2),3).ToList();
        var actual=new Consumer<int>();
        LinqChain2TestBase.Source().Skip(2).Take(3).Consume(actual);
        LinqChain2TestBase.AssertSequenceEqual(expected,actual.Items);
    }
}
