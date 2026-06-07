using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Where_Where:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(LinqChain2TestBase.Source(),static v => v>1),static v => v<6).ToList();
        var actual=new Consumer<int>();
        LinqChain2TestBase.Source().Where(static v => v>1).Where(static v => v<6).Consume(actual);
        LinqChain2TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() => LambdaImpl();

    private static void LambdaImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(LinqChain2TestBase.Source(),v => v>1),v => v<6).ToList();
        var actual=new Consumer<int>();
        LinqChain2TestBase.Source().Where(v => v>1).Where(v => v<6).Consume(actual);
        LinqChain2TestBase.AssertSequenceEqual(expected,actual.Items);
    }
}
