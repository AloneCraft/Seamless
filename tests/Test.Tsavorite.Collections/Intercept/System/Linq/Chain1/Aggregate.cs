using System.Linq;

using Tsavorite;
using Tsavorite.SourceGenerator;
// ReSharper disable ConvertClosureToMethodGroup
//直接指定するとインターセプトされない

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Aggregate(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void func_Static() {
        var expected=Source().Aggregate(static (l,r) => l+r);
        AssertOptimized(nameof(func_StaticOptimized),expected,()=>func_StaticOptimized(),3,0);
        AssertOptimized(nameof(func_StaticNoOptimized),expected,()=>func_StaticNoOptimized(),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int func_StaticOptimized() =>
        Source().Aggregate(static (l,r) => l+r);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int func_StaticNoOptimized() =>
        Source().Aggregate(static (l,r) => l+r);

    [Fact, Trait("Category","Unit")]
    public void func_Lambda() {
        var expected=Source().Aggregate((l,r) => l+r);
        AssertOptimized(expected,()=>func_LambdaOptimized(),3,0);
        AssertOptimized(expected,()=>func_LambdaNoOptimized(),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int func_LambdaOptimized() =>
        Source().Aggregate((l,r) => l+r);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int func_LambdaNoOptimized() =>
        Source().Aggregate((l,r) => l+r);

    [Fact, Trait("Category","Unit")]
    public void func_Captured() {
        var offset=0;
        var expected=Source().Aggregate((l,r) => l+r+offset);
        AssertOptimized(expected,()=>func_CapturedOptimized(offset),3,0);
        AssertOptimized(expected,()=>func_CapturedNoOptimized(offset),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int func_CapturedOptimized(int offset) =>
        Source().Aggregate((l,r) => l+r+offset);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int func_CapturedNoOptimized(int offset) =>
        Source().Aggregate((l,r) => l+r+offset);

    [Fact, Trait("Category","Unit")]
    public void func_DelegateSyntax() {
        var expected=Source().Aggregate(delegate(int l,int r) { return l+r; });
        AssertOptimized(expected,()=>func_DelegateSyntaxOptimized(),3,0);
        AssertOptimized(expected,()=>func_DelegateSyntaxNoOptimized(),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int func_DelegateSyntaxOptimized() =>
        Source().Aggregate(delegate(int l,int r) { return l+r; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int func_DelegateSyntaxNoOptimized() =>
        Source().Aggregate(delegate(int l,int r) { return l+r; });

    [Fact, Trait("Category","Unit")]
    public void func_Variable() {
        Func<int,int,int> func=(l,r) => l+r;
        var expected=Source().Aggregate(func);
        AssertOptimized(expected,()=>func_VariableOptimized(func),3,3);
        AssertOptimized(expected,()=>func_VariableNoOptimized(func),3,3);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int func_VariableOptimized(Func<int,int,int> func) =>
        Source().Aggregate(func);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int func_VariableNoOptimized(Func<int,int,int> func) =>
        Source().Aggregate(func);

    [Fact, Trait("Category","Unit")]
    public void seed_func_Static() {
        var expected=Source().Aggregate(0,static (sum,v) => sum+v);
        AssertOptimized(expected,()=>seed_func_StaticOptimized(),4,0);
        AssertOptimized(expected,()=>seed_func_StaticNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_StaticOptimized() =>
        Source().Aggregate(0,static (sum,v) => sum+v);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_StaticNoOptimized() =>
        Source().Aggregate(0,static (sum,v) => sum+v);

    [Fact, Trait("Category","Unit")]
    public void seed_func_Lambda() {
        var expected=Source().Aggregate(0,(sum,v) => sum+v);
        AssertOptimized(expected,()=>seed_func_LambdaOptimized(),4,0);
        AssertOptimized(expected,()=>seed_func_LambdaNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_LambdaOptimized() =>
        Source().Aggregate(0,(sum,v) => sum+v);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_LambdaNoOptimized() =>
        Source().Aggregate(0,(sum,v) => sum+v);

    [Fact, Trait("Category","Unit")]
    public void seed_func_Captured() {
        var offset=0;
        var expected=Source().Aggregate(0,(sum,v) => sum+v+offset);
        AssertOptimized(expected,()=>seed_func_CapturedOptimized(offset),4,0);
        AssertOptimized(expected,()=>seed_func_CapturedNoOptimized(offset),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_CapturedOptimized(int offset) =>
        Source().Aggregate(0,(sum,v) => sum+v+offset);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_CapturedNoOptimized(int offset) =>
        Source().Aggregate(0,(sum,v) => sum+v+offset);

    [Fact, Trait("Category","Unit")]
    public void seed_func_DelegateSyntax() {
        var expected=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; });
        AssertOptimized(expected,()=>seed_func_DelegateSyntaxOptimized(),4,0);
        AssertOptimized(expected,()=>seed_func_DelegateSyntaxNoOptimized(),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_DelegateSyntaxOptimized() =>
        Source().Aggregate(0,delegate(int sum,int v) { return sum+v; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_DelegateSyntaxNoOptimized() =>
        Source().Aggregate(0,delegate(int sum,int v) { return sum+v; });

    [Fact, Trait("Category","Unit")]
    public void seed_func_Variable() {
        Func<int,int,int> func=(sum,v) => sum+v;
        var expected=Source().Aggregate(0,func);
        AssertOptimized(expected,()=>seed_func_VariableOptimized(func),4,4);
        AssertOptimized(expected,()=>seed_func_VariableNoOptimized(func),4,4);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_VariableOptimized(Func<int,int,int> func) =>
        Source().Aggregate(0,func);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_VariableNoOptimized(Func<int,int,int> func) =>
        Source().Aggregate(0,func);

    [Fact, Trait("Category","Unit")]
    public void seed_func_resultSelector_Static() {
        var expected=Source().Aggregate(0,static (sum,v) => sum+v,static sum => sum*2);
        AssertOptimized(expected,()=>seed_func_resultSelector_StaticOptimized(),5,0);
        AssertOptimized(expected,()=>seed_func_resultSelector_StaticNoOptimized(),5,5);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_StaticOptimized() =>
        Source().Aggregate(0,static (sum,v) => sum+v,static sum => sum*2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_StaticNoOptimized() =>
        Source().Aggregate(0,static (sum,v) => sum+v,static sum => sum*2);

    [Fact, Trait("Category","Unit")]
    public void seed_func_resultSelector_Lambda() {
        var expected=Source().Aggregate(0,(sum,v) => sum+v,sum => sum*2);
        AssertOptimized(expected,()=>seed_func_resultSelector_LambdaOptimized(),5,0);
        AssertOptimized(expected,()=>seed_func_resultSelector_LambdaNoOptimized(),5,5);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_LambdaOptimized() =>
        Source().Aggregate(0,(sum,v) => sum+v,sum => sum*2);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_LambdaNoOptimized() =>
        Source().Aggregate(0,(sum,v) => sum+v,sum => sum*2);

    [Fact, Trait("Category","Unit")]
    public void seed_func_resultSelector_Captured() {
        var offset=0;
        var expected=Source().Aggregate(0,(sum,v) => sum+v+offset,sum => sum*2+offset);
        AssertOptimized(expected,()=>seed_func_resultSelector_CapturedOptimized(offset),5,0);
        AssertOptimized(expected,()=>seed_func_resultSelector_CapturedNoOptimized(offset),5,5);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_CapturedOptimized(int offset) =>
        Source().Aggregate(0,(sum,v) => sum+v+offset,sum => sum*2+offset);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_CapturedNoOptimized(int offset) =>
        Source().Aggregate(0,(sum,v) => sum+v+offset,sum => sum*2+offset);

    [Fact, Trait("Category","Unit")]
    public void seed_func_resultSelector_DelegateSyntax() {
        var expected=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });
        AssertOptimized(expected,()=>seed_func_resultSelector_DelegateSyntaxOptimized(),5,0);
        AssertOptimized(expected,()=>seed_func_resultSelector_DelegateSyntaxNoOptimized(),5,5);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_DelegateSyntaxOptimized() =>
        Source().Aggregate(0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_DelegateSyntaxNoOptimized() =>
        Source().Aggregate(0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });

    [Fact, Trait("Category","Unit")]
    public void seed_func_resultSelector_Variable() {
        Func<int,int,int> func=(sum,v) => sum+v;
        Func<int,int> resultSelector=sum => sum*2;
        var expected=Source().Aggregate(0,func,resultSelector);
        AssertOptimized(expected,()=>seed_func_resultSelector_VariableOptimized(func,resultSelector),5,5);
        AssertOptimized(expected,()=>seed_func_resultSelector_VariableNoOptimized(func,resultSelector),5,5);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_VariableOptimized(Func<int,int,int> func,Func<int,int> resultSelector) =>
        Source().Aggregate(0,func,resultSelector);

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int seed_func_resultSelector_VariableNoOptimized(Func<int,int,int> func,Func<int,int> resultSelector) =>
        Source().Aggregate(0,func,resultSelector);


}
