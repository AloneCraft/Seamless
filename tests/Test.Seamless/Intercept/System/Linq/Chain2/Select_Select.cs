using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Select_Select:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),static v => v*2),static v => v+1).ToList();
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Select(static v => v+1).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() => LambdaImpl();

    private static void LambdaImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),v => v*2),v => v+1).ToList();
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Select(v => v+1).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() => QueryImpl();

    private static void QueryImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),v => v*2),v => v+1).ToList();
        var actual=new Consumer<int>();
        (from v in Source() select v*2 into doubled select doubled+1).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
