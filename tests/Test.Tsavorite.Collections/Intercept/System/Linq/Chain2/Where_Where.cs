using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class Where_Where:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() {
        var expected=Source().Where(static v => v>1).Where(static v => v<6).ToList();
        StaticOptimized(expected);
        StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(static v => v>1).Where(static v => v<6).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(static v => v>1).Where(static v => v<6).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() {
        var expected=Source().Where(v => v>1).Where(v => v<6).ToList();
        LambdaOptimized(expected);
        LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(v => v>1).Where(v => v<6).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(v => v>1).Where(v => v<6).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() {
        var expected=Source().Where(v => v>1).Where(v => v<6).ToList();
        QueryOptimized(expected);
        QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() where v>1 where v<6 select v).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() where v>1 where v<6 select v).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
