using System.Linq;

using Seamless.FLinq;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Any(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=Source().Any();
        AssertOptimized(expected,()=>NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool NoDelegateOptimized() =>
        Source().Any();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool NoDelegateNoOptimized() =>
        Source().Any();

    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected=Source().Any(static v => v>3);
        AssertOptimized(expected,()=>predicate_StaticOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_StaticNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_StaticOptimized() =>
        Source().Any(static v => v>3);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_StaticNoOptimized() =>
        Source().Any(static v => v>3);

    [Fact, Trait("Category","Unit")]
    public void predicate_Nested() {
        var expected=Source().Any(p => Source().Any(q => q==p));
        AssertOptimized(expected,()=>predicate_NestedOptimized(),1,0);
        AssertOptimized(expected,()=>predicate_NestedNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_NestedOptimized() =>
        Source().Any(p => Source().Any(q => q==p));

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_NestedNoOptimized() =>
        Source().Any(p => Source().Any(q => q==p));

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected=Source().Any(v => v>3);
        AssertOptimized(expected,()=>predicate_LambdaOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_LambdaOptimized() =>
        Source().Any(v => v>3);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_LambdaNoOptimized() =>
        Source().Any(v => v>3);

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold=3;
        var expected=Source().Any(v => v>threshold);
        AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),4,0);
        AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_CapturedOptimized(int threshold) =>
        Source().Any(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_CapturedNoOptimized(int threshold) =>
        Source().Any(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected=Source().Any(delegate(int v) { return v>3; });
        AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_DelegateSyntaxOptimized() =>
        Source().Any(delegate(int v) { return v>3; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_DelegateSyntaxNoOptimized() =>
        Source().Any(delegate(int v) { return v>3; });

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate=v => v>3;
        var expected=Source().Any(predicate);
        AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),4,4);
        AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().Any(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().Any(predicate);
}
