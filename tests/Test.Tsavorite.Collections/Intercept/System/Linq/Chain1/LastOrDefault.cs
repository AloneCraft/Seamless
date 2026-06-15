using System.Linq;

using Tsavorite.SourceGenerator;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class LastOrDefault:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=Source().LastOrDefault();
        AssertOptimized(expected,()=>NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int NoDelegateOptimized() =>
        Source().LastOrDefault();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int NoDelegateNoOptimized() =>
        Source().LastOrDefault();

    [Fact, Trait("Category","Unit")]
    public static void defaultValue() {
        var expected=EmptySource().LastOrDefault(42);
        AssertOptimized(expected,()=>defaultValueOptimized(),0,0);
        AssertOptimized(expected,()=>defaultValueNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int defaultValueOptimized() =>
        EmptySource().LastOrDefault(42);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int defaultValueNoOptimized() =>
        EmptySource().LastOrDefault(42);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() {
        var expected=Source().LastOrDefault(static v => v>2);
        AssertOptimized(expected,()=>predicate_StaticOptimized(),1,0);
        AssertOptimized(expected,()=>predicate_StaticNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_StaticOptimized() =>
        Source().LastOrDefault(static v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_StaticNoOptimized() =>
        Source().LastOrDefault(static v => v>2);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() {
        var expected=Source().LastOrDefault(v => v>2);
        AssertOptimized(expected,()=>predicate_LambdaOptimized(),1,0);
        AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_LambdaOptimized() =>
        Source().LastOrDefault(v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_LambdaNoOptimized() =>
        Source().LastOrDefault(v => v>2);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() {
        var threshold=2;
        var expected=Source().LastOrDefault(v => v>threshold);
        AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),1,0);
        AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_CapturedOptimized(int threshold) =>
        Source().LastOrDefault(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_CapturedNoOptimized(int threshold) =>
        Source().LastOrDefault(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() {
        var expected=Source().LastOrDefault(delegate(int v) { return v>2; });
        AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),1,0);
        AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxOptimized() =>
        Source().LastOrDefault(delegate(int v) { return v>2; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxNoOptimized() =>
        Source().LastOrDefault(delegate(int v) { return v>2; });

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=Source().LastOrDefault(predicate);
        AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),1,1);
        AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().LastOrDefault(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().LastOrDefault(predicate);
}
