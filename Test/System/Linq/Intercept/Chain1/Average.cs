using System.Linq;

using Seamless.Tsavorite.Collections;
using Seamless.FLinq;

namespace Test.System.Linq.Intercept.Chain1;

public sealed class Average(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void int_NoDelegate() {
        var expected=Source().Average();
        AssertOptimized(expected,()=>int_NoDelegateOptimized(),0,0);
        AssertOptimized(expected,()=>int_NoDelegateNoOptimized(),0,0);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double int_NoDelegateOptimized() =>
        Source().Average();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double int_NoDelegateNoOptimized() =>
        Source().Average();

    [Fact, Trait("Category","Unit")]
    public void selector_Nested() {
        var expected=Source().Average<int>(p => Source().Average(int (q) => q+p));
        AssertOptimized(expected,()=>selector_NestedOptimized(),4,0);
        AssertOptimized(expected,()=>selector_NestedNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double selector_NestedOptimized() =>
        Source().Average(p => Source().Average(q => q+p));

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double selector_NestedNoOptimized() =>
        Source().Average(p => Source().Average(q => q+p));

    [Fact, Trait("Category","Unit")]
    public void selector_Static() {
        var expected=Rows().Average(static row => row.Value);
        AssertOptimized(expected,()=>selector_StaticOptimized(),4,0);
        AssertOptimized(expected,()=>selector_StaticNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double selector_StaticOptimized() =>
        Rows().Average(static row => row.Value);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double selector_StaticNoOptimized() =>
        Rows().Average(static row => row.Value);

    [Fact, Trait("Category","Unit")]
    public void selector_Lambda() {
        var expected=Rows().Average(row => row.Value);
        AssertOptimized(expected,()=>selector_LambdaOptimized(),4,0);
        AssertOptimized(expected,()=>selector_LambdaNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double selector_LambdaOptimized() =>
        Rows().Average(row => row.Value);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double selector_LambdaNoOptimized() =>
        Rows().Average(row => row.Value);

    [Fact, Trait("Category","Unit")]
    public void selector_Captured() {
        var offset=0;
        var expected=Rows().Average(row => row.Value+offset);
        AssertOptimized(expected,()=>selector_CapturedOptimized(offset),4,0);
        AssertOptimized(expected,()=>selector_CapturedNoOptimized(offset),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double selector_CapturedOptimized(int offset) =>
        Rows().Average(row => row.Value+offset);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double selector_CapturedNoOptimized(int offset) =>
        Rows().Average(row => row.Value+offset);

    [Fact, Trait("Category","Unit")]
    public void selector_DelegateSyntax() {
        var expected=Rows().Average(delegate(Chain1Row row) { return row.Value; });
        AssertOptimized(expected,()=>selector_DelegateSyntaxOptimized(),4,0);
        AssertOptimized(expected,()=>selector_DelegateSyntaxNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double selector_DelegateSyntaxOptimized() =>
        Rows().Average(delegate(Chain1Row row) { return row.Value; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double selector_DelegateSyntaxNoOptimized() =>
        Rows().Average(delegate(Chain1Row row) { return row.Value; });

    [Fact, Trait("Category","Unit")]
    public void selector_Variable() {
        Func<Chain1Row,int> selector=row => row.Value;
        var expected=Rows().Average(selector);
        AssertOptimized(expected,()=>selector_VariableOptimized(selector),4,4);
        AssertOptimized(expected,()=>selector_VariableNoOptimized(selector),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static double selector_VariableOptimized(Func<Chain1Row,int> selector) =>
        Rows().Average(selector);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static double selector_VariableNoOptimized(Func<Chain1Row,int> selector) =>
        Rows().Average(selector);

}
