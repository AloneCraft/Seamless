using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class MaxBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.MaxBy(LinqChain1TestBase.Rows(),static row => row.Key);
        var actual=LinqChain1TestBase.Rows().MaxBy(static row => row.Key);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.MaxBy(LinqChain1TestBase.Rows(),row => row.Key);
        var actual=LinqChain1TestBase.Rows().MaxBy(row => row.Key);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.MaxBy(LinqChain1TestBase.Rows(),row => row.Key+offset);
        var actual=LinqChain1TestBase.Rows().MaxBy(row => row.Key+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.MaxBy(LinqChain1TestBase.Rows(),delegate(Chain1Row row) { return row.Key; });
        var actual=LinqChain1TestBase.Rows().MaxBy(delegate(Chain1Row row) { return row.Key; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.MaxBy(LinqChain1TestBase.Rows(),keySelector);
        var actual=LinqChain1TestBase.Rows().MaxBy(keySelector);
        Assert.Equal(expected,actual);
    }

}
