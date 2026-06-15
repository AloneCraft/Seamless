using System.Linq;

using Tsavorite.SourceGenerator;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Last:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=Source().Last();
        AssertOptimized(expected,()=>NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int NoDelegateOptimized() =>
        Source().Last();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int NoDelegateNoOptimized() =>
        Source().Last();

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() {
        var expected=Source().Last(static v => v>2);
        AssertOptimized(expected,()=>predicate_StaticOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_StaticNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_StaticOptimized() =>
        Source().Last(static v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_StaticNoOptimized() =>
        Source().Last(static v => v>2);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Nested() {
        var expected=Source().Last(p => Source().Last(q => q==p)==p);
        AssertOptimized(expected,()=>predicate_NestedOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_NestedNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_NestedOptimized() =>
        Source().Last(p => Source().Last(q => q==p)==p);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_NestedNoOptimized() =>
        Source().Last(p => Source().Last(q => q==p)==p);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() {
        var expected=Source().Last(v => v>2);
        AssertOptimized(expected,()=>predicate_LambdaOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_LambdaOptimized() =>
        Source().Last(v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_LambdaNoOptimized() =>
        Source().Last(v => v>2);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() {
        var threshold=2;
        var expected=Source().Last(v => v>threshold);
        AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),4,0);
        AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_CapturedOptimized(int threshold) =>
        Source().Last(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_CapturedNoOptimized(int threshold) =>
        Source().Last(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() {
        var expected=Source().Last(delegate(int v) { return v>2; });
        AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxOptimized() =>
        Source().Last(delegate(int v) { return v>2; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxNoOptimized() =>
        Source().Last(delegate(int v) { return v>2; });

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=Source().Last(predicate);
        AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),4,4);
        AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().Last(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().Last(predicate);
}
