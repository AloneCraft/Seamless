using System.Linq;

using Tsavorite.SourceGenerator;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Count:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() {
        var expected=Source().Count();
        AssertOptimized(expected,()=>NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int NoDelegateOptimized() =>
        Source().Count();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int NoDelegateNoOptimized() =>
        Source().Count();

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() {
        var expected=Source().Count(static v => v>2);
        AssertOptimized(expected,()=>predicate_StaticOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_StaticNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_StaticOptimized() =>
        Source().Count(static v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_StaticNoOptimized() =>
        Source().Count(static v => v>2);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Nested() {
        var expected=Source().Count(p => Source().Count(q => q==p)>0);
        AssertOptimized(expected,()=>predicate_NestedOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_NestedNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_NestedOptimized() =>
        Source().Count(p => Source().Count(q => q==p)>0);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_NestedNoOptimized() =>
        Source().Count(p => Source().Count(q => q==p)>0);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() {
        var expected=Source().Count(v => v>2);
        AssertOptimized(expected,()=>predicate_LambdaOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_LambdaOptimized() =>
        Source().Count(v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_LambdaNoOptimized() =>
        Source().Count(v => v>2);

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() {
        var threshold=2;
        var expected=Source().Count(v => v>threshold);
        AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),4,0);
        AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_CapturedOptimized(int threshold) =>
        Source().Count(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_CapturedNoOptimized(int threshold) =>
        Source().Count(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() {
        var expected=Source().Count(delegate(int v) { return v>2; });
        AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxOptimized() =>
        Source().Count(delegate(int v) { return v>2; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxNoOptimized() =>
        Source().Count(delegate(int v) { return v>2; });

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=Source().Count(predicate);
        AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),4,4);
        AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().Count(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().Count(predicate);
}
