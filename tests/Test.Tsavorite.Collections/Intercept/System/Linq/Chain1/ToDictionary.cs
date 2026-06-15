using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class ToDictionary:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() {
        var expected=Rows().Take(3).ToDictionary(static row => row.Key);
        keySelector_StaticOptimized(expected);
        keySelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key);
        AssertDictionaryEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key);
        AssertDictionaryEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_elementSelector_Static() {
        var expected=Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value);
        keySelector_elementSelector_StaticOptimized(expected);
        keySelector_elementSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_StaticOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value);
        AssertDictionaryEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_StaticNoOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value);
        AssertDictionaryEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() {
        var expected=Rows().Take(3).ToDictionary(static row => row.Key,EqualityComparer<int>.Default);
        keySelector_comparerOptimized(expected);
        keySelector_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key,EqualityComparer<int>.Default);
        AssertDictionaryEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_comparerNoOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key,EqualityComparer<int>.Default);
        AssertDictionaryEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_elementSelector_comparer() {
        var expected=Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        keySelector_elementSelector_comparerOptimized(expected);
        keySelector_elementSelector_comparerNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_comparerOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        AssertDictionaryEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void keySelector_elementSelector_comparerNoOptimized(dynamic expected) {
        var actual=Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        AssertDictionaryEqual(expected,actual);
    }

}
