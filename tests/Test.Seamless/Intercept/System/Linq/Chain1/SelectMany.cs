using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class SelectMany:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void selector_Static() => selector_StaticImpl();

    private static void selector_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),static v => new[] { v,v+10 }).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(static v => new[] { v,v+10 }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_index_Static() => selector_index_StaticImpl();

    private static void selector_index_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),static (v,i) => new[] { v,i }).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(static (v,i) => new[] { v,i }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void collectionSelector_resultSelector_Static() => collectionSelector_resultSelector_StaticImpl();

    private static void collectionSelector_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),static v => new[] { v,v+10 },static (v,sub) => sub+v).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub+v).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void collectionSelector_index_resultSelector_Static() => collectionSelector_index_resultSelector_StaticImpl();

    private static void collectionSelector_index_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),static (v,i) => new[] { v,i },static (v,sub) => sub+v).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(static (v,i) => new[] { v,i },static (v,sub) => sub+v).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Lambda() => selector_LambdaImpl();

    private static void selector_LambdaImpl() {
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),v => new[] { v,v+10 }).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(v => new[] { v,v+10 }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Captured() => selector_CapturedImpl();

    private static void selector_CapturedImpl() {
        var offset=10;
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),v => new[] { v,v+offset }).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(v => new[] { v,v+offset }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_DelegateSyntax() => selector_DelegateSyntaxImpl();

    private static void selector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),delegate(int v) { return new[] { v,v+10 }; }).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_Variable() => selector_VariableImpl();

    private static void selector_VariableImpl() {
        Func<int,int[]> selector=v => new[] { v,v+10 };
        var expected=SystemEnumerable.SelectMany(LinqChain1TestBase.Source(),selector).ToList();
        var actual=LinqChain1TestBase.Source().SelectMany(selector).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
