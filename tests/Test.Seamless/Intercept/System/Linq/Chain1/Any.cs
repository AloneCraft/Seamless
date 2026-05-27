using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Any:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.Any(LinqChain1TestBase.Source());
        var actual=LinqChain1TestBase.Source().Any();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.Any(LinqChain1TestBase.Source(),static v => v>3);
        var actual=LinqChain1TestBase.Source().Any(static v => v>3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.Any(LinqChain1TestBase.Source(),v => v>3);
        var actual=LinqChain1TestBase.Source().Any(v => v>3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=3;
        var expected=SystemEnumerable.Any(LinqChain1TestBase.Source(),v => v>threshold);
        var actual=LinqChain1TestBase.Source().Any(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Any(LinqChain1TestBase.Source(),delegate(int v) { return v>3; });
        var actual=LinqChain1TestBase.Source().Any(delegate(int v) { return v>3; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v>3;
        var expected=SystemEnumerable.Any(LinqChain1TestBase.Source(),predicate);
        var actual=LinqChain1TestBase.Source().Any(predicate);
        Assert.Equal(expected,actual);
    }

}
