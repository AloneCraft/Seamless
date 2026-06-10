using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class IntersectBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.IntersectBy(Rows(),Second(),static row => row.Key).ToList();
        var actual=Rows().IntersectBy(Second(),static row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.IntersectBy(Rows(),Second(),row => row.Key).ToList();
        var actual=Rows().IntersectBy(Second(),row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.IntersectBy(Rows(),Second(),row => row.Key+offset).ToList();
        var actual=Rows().IntersectBy(Second(),row => row.Key+offset).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.IntersectBy(Rows(),Second(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=Rows().IntersectBy(Second(),delegate(Chain1Row row) { return row.Key; }).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.IntersectBy(Rows(),Second(),keySelector).ToList();
        var actual=Rows().IntersectBy(Second(),keySelector).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
