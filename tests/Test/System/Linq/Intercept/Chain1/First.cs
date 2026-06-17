using System.Linq;

using Tsavorite.SourceGenerator;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class First(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=Source().First();
        AssertOptimized(expected,()=>NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int NoDelegateOptimized() =>
        Source().First();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int NoDelegateNoOptimized() =>
        Source().First();

    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected=Source().First(static v => v>2);
        AssertOptimized(expected,()=>predicate_StaticOptimized(),3,0);
        AssertOptimized(expected,()=>predicate_StaticNoOptimized(),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_StaticOptimized() =>
        Source().First(static v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_StaticNoOptimized() =>
        Source().First(static v => v>2);

    [Fact, Trait("Category","Unit")]
    public void predicate_Nested() {
        var expected=Source().First(p => Source().First(q => q==p)==p);
        AssertOptimized(expected,()=>predicate_NestedOptimized(),1,0);
        AssertOptimized(expected,()=>predicate_NestedNoOptimized(),1,1);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_NestedOptimized() =>
        Source().First(p => Source().First(q => q==p)==p);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_NestedNoOptimized() =>
        Source().First(p => Source().First(q => q==p)==p);

    [Fact, Trait("Category","Unit")]
    public void predicate_Lambda() {
        var expected=Source().First(v => v>2);
        AssertOptimized(expected,()=>predicate_LambdaOptimized(),3,0);
        AssertOptimized(expected,()=>predicate_LambdaNoOptimized(),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_LambdaOptimized() =>
        Source().First(v => v>2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_LambdaNoOptimized() =>
        Source().First(v => v>2);

    [Fact, Trait("Category","Unit")]
    public void predicate_Captured() {
        var threshold=2;
        var expected=Source().First(v => v>threshold);
        AssertOptimized(expected,()=>predicate_CapturedOptimized(threshold),3,0);
        AssertOptimized(expected,()=>predicate_CapturedNoOptimized(threshold),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_CapturedOptimized(int threshold) =>
        Source().First(v => v>threshold);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_CapturedNoOptimized(int threshold) =>
        Source().First(v => v>threshold);

    [Fact, Trait("Category","Unit")]
    public void predicate_DelegateSyntax() {
        var expected=Source().First(delegate(int v) { return v>2; });
        AssertOptimized(expected,()=>predicate_DelegateSyntaxOptimized(),3,0);
        AssertOptimized(expected,()=>predicate_DelegateSyntaxNoOptimized(),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxOptimized() =>
        Source().First(delegate(int v) { return v>2; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_DelegateSyntaxNoOptimized() =>
        Source().First(delegate(int v) { return v>2; });

    [Fact, Trait("Category","Unit")]
    public void predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=Source().First(predicate);
        AssertOptimized(expected,()=>predicate_VariableOptimized(predicate),3,3);
        AssertOptimized(expected,()=>predicate_VariableNoOptimized(predicate),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int predicate_VariableOptimized(Func<int,bool> predicate) =>
        Source().First(predicate);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int predicate_VariableNoOptimized(Func<int,bool> predicate) =>
        Source().First(predicate);
}
