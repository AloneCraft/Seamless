using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class All:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.All(LinqChain1TestBase.Source(),static v => v>0);
        var actual=LinqChain1TestBase.Source().All(static v => v>0);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.All(LinqChain1TestBase.Source(),v => v>0);
        var actual=LinqChain1TestBase.Source().All(v => v>0);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=0;
        var expected=SystemEnumerable.All(LinqChain1TestBase.Source(),v => v>threshold);
        var actual=LinqChain1TestBase.Source().All(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.All(LinqChain1TestBase.Source(),delegate(int v) { return v>0; });
        var actual=LinqChain1TestBase.Source().All(delegate(int v) { return v>0; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v>0;
        var expected=SystemEnumerable.All(LinqChain1TestBase.Source(),predicate);
        var actual=LinqChain1TestBase.Source().All(predicate);
        Assert.Equal(expected,actual);
    }

}
