using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain2;

public sealed class Where_Where:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),static v => v>1),static v => v<6).ToList();
        var actual=new Consumer<int>();
        Source().Where(static v => v>1).Where(static v => v<6).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() => LambdaImpl();

    private static void LambdaImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),v => v>1),v => v<6).ToList();
        var actual=new Consumer<int>();
        Source().Where(v => v>1).Where(v => v<6).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() => QueryImpl();

    private static void QueryImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),v => v>1),v => v<6).ToList();
        var actual=new Consumer<int>();
        (from v in Source() where v>1 where v<6 select v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
