using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Select_Where:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),static v => v*2),static v => v>5).ToList();
        var actual=new Consumer<int>();
        Source().Select(static v => v*2).Where(static v => v>5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() => LambdaImpl();

    private static void LambdaImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),v => v*2),v => v>5).ToList();
        var actual=new Consumer<int>();
        Source().Select(v => v*2).Where(v => v>5).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() => QueryImpl();

    private static void QueryImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),v => v*2),v => v>5).ToList();
        var actual=new Consumer<int>();
        (from v in Source() select v*2 into doubled where doubled>5 select doubled).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
