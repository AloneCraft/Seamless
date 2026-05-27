using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Single:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Single(new[] { 1 });
        var actual=new[] { 1 }.Single();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.Single(LinqChain1TestBase.Source(),static v => v==3);
        var actual=LinqChain1TestBase.Source().Single(static v => v==3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.Single(LinqChain1TestBase.Source(),v => v==3);
        var actual=LinqChain1TestBase.Source().Single(v => v==3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=2;
        var expected=SystemEnumerable.Single(LinqChain1TestBase.Source(),v => v>threshold);
        var actual=LinqChain1TestBase.Source().Single(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Single(LinqChain1TestBase.Source(),delegate(int v) { return v==3; });
        var actual=LinqChain1TestBase.Source().Single(delegate(int v) { return v==3; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v==3;
        var expected=SystemEnumerable.Single(LinqChain1TestBase.Source(),predicate);
        var actual=LinqChain1TestBase.Source().Single(predicate);
        Assert.Equal(expected,actual);
    }

}
