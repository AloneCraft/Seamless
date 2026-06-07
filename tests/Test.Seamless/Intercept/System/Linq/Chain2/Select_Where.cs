using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Select_Where:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(LinqChain2TestBase.Source(),static v => v*2),static v => v>5).ToList();
        var actual=new Consumer<int>();
        LinqChain2TestBase.Source().Select(static v => v*2).Where(static v => v>5).Consume(actual);
        LinqChain2TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() => LambdaImpl();

    private static void LambdaImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(LinqChain2TestBase.Source(),v => v*2),v => v>5).ToList();
        var actual=new Consumer<int>();
        LinqChain2TestBase.Source().Select(v => v*2).Where(v => v>5).Consume(actual);
        LinqChain2TestBase.AssertSequenceEqual(expected,actual.Items);
    }
}
