using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class Where_Select:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() {
        var expected=Source().Where(static v => v>2).Select(static v => v*10).ToList();
        StaticOptimized(expected);
        StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(static v => v>2).Select(static v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(static v => v>2).Select(static v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() {
        var expected=Source().Where(v => v>2).Select(v => v*10).ToList();
        QueryOptimized(expected);
        QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() where v>2 select v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() where v>2 select v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() {
        var expected=Source().Where(v => v>2).Select(v => v*10).ToList();
        LambdaOptimized(expected);
        LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(v => v>2).Select(v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Where(v => v>2).Select(v => v*10).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
