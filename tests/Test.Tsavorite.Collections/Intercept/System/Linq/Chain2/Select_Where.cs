using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed class Select_Where:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() {
        var expected=Source().Select(static v => v*2).Where(static v => v>5).ToList();
        StaticOptimized(expected);
        StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(static v => v*2).Where(static v => v>5).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(static v => v*2).Where(static v => v>5).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Lambda() {
        var expected=Source().Select(v => v*2).Where(v => v>5).ToList();
        LambdaOptimized(expected);
        LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(v => v*2).Where(v => v>5).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Select(v => v*2).Where(v => v>5).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void Query() {
        var expected=Source().Select(v => v*2).Where(v => v>5).ToList();
        QueryOptimized(expected);
        QueryNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void QueryOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() select v*2 into doubled where doubled>5 select doubled).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void QueryNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                (from v in Source() select v*2 into doubled where doubled>5 select doubled).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
