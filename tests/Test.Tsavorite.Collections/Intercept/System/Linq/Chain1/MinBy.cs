using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class MinBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.MinBy(Rows(),static row => row.Key);
        var actual=Rows().MinBy(static row => row.Key);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.MinBy(Rows(),row => row.Key);
        var actual=Rows().MinBy(row => row.Key);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.MinBy(Rows(),row => row.Key+offset);
        var actual=Rows().MinBy(row => row.Key+offset);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.MinBy(Rows(),delegate(Chain1Row row) { return row.Key; });
        var actual=Rows().MinBy(delegate(Chain1Row row) { return row.Key; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.MinBy(Rows(),keySelector);
        var actual=Rows().MinBy(keySelector);
        Assert.Equal(expected,actual);
    }

}
