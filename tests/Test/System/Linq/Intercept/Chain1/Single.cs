using System.Linq;

using Tsavorite.SourceGenerator;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Single(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=new[]{1}.Single();
        AssertOptimized(expected,()=>NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int NoDelegateOptimized() =>
        new[] { 1 }.Single();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int NoDelegateNoOptimized() =>
        new[] { 1 }.Single();

    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected=Source().Single(static v => v==3);
        AssertOptimized(expected,()=>predicate_StaticOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_StaticNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_StaticOptimized() =>
        Source().Single(static v => v==3);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_StaticNoOptimized() =>
        Source().Single(static v => v==3);

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected=Source().Single(v => v==3);
        AssertOptimized(expected,()=>predicate_LambdaOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_LambdaOptimized() =>
        Source().Single(v => v==3);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_LambdaNoOptimized() =>
        Source().Single(v => v==3);

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold=3;
        var expected=Source().Single(v => v>threshold);
        AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),4,0);
        AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_CapturedOptimized(int threshold) =>
        Source().Single(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_CapturedNoOptimized(int threshold) =>
        Source().Single(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected=Source().Single(delegate(int v) { return v==3; });
        AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),4,0);
        AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxOptimized() =>
        Source().Single(delegate(int v) { return v==3; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxNoOptimized() =>
        Source().Single(delegate(int v) { return v==3; });

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate=v => v==3;
        var expected=Source().Single(predicate);
        AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),4,4);
        AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().Single(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().Single(predicate);
}
