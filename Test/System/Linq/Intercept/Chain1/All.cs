using System.Linq;

using Seamless.FLinq;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class All(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected=Source().All(static v => v>0);
        this.AssertOptimized(expected,()=>predicate_StaticOptimized(),4,0);
        this.AssertOptimized(expected,()=>predicate_StaticNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_StaticOptimized() =>
        Source().All(static v => v>0);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_StaticNoOptimized() =>
        Source().All(static v => v>0);

    [Fact, Trait("Category","Unit")]
    public void predicate_Nested() {
        var expected=Source().All(p => Source().All(q => q>0));
        this.AssertOptimized(expected,()=>predicate_NestedOptimized(),4,0);
        this.AssertOptimized(expected,()=>predicate_NestedNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_NestedOptimized() =>
        Source().All(p => Source().All(q => q>0));

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_NestedNoOptimized() =>
        Source().All(p => Source().All(q => q>0));

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected=Source().All(v => v>0);
        this.AssertOptimized(expected,()=>predicate_LambdaOptimized(),4,0);
        this.AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_LambdaOptimized() =>
        Source().All(v => v>0);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_LambdaNoOptimized() =>
        Source().All(v => v>0);

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold=0;
        var expected=Source().All(v => v>threshold);
        this.AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),4,0);
        this.AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_CapturedOptimized(int threshold) =>
        Source().All(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_CapturedNoOptimized(int threshold) =>
        Source().All(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected=Source().All(delegate(int v) { return v>0; });
        this.AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),4,0);
        this.AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_DelegateSyntaxOptimized() =>
        Source().All(delegate(int v) { return v>0; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_DelegateSyntaxNoOptimized() =>
        Source().All(delegate(int v) { return v>0; });

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate=v => v>0;
        var expected=Source().All(predicate);
        this.AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),4,4);
        this.AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().All(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static bool predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().All(predicate);
}
