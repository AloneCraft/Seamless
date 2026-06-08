using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

public sealed class Where_Select:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static v => v>2),static v => v*10).ToList();
        var actual=new Consumer<int>();
        Source().Where(static v => v>2).Select(static v => v*10).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() => QueryImpl();

    private static void QueryImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),v => v>2),v => v*10).ToList();
        var actual=new Consumer<int>();
        (from v in Source() where v>2 select v*10).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() => LambdaImpl();

    private static void LambdaImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),v => v>2),v => v*10).ToList();
        var actual=new Consumer<int>();
        Source().Where(v => v>2).Select(v => v*10).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
