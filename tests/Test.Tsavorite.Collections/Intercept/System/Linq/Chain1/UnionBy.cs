using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class UnionBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.UnionBy(Rows(),OtherRows(),static row => row.Key).ToList();
        var actual=Rows().UnionBy(OtherRows(),static row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.UnionBy(Rows(),OtherRows(),row => row.Key).ToList();
        var actual=Rows().UnionBy(OtherRows(),row => row.Key).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.UnionBy(Rows(),OtherRows(),row => row.Key+offset).ToList();
        var actual=Rows().UnionBy(OtherRows(),row => row.Key+offset).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.UnionBy(Rows(),OtherRows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=Rows().UnionBy(OtherRows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.UnionBy(Rows(),OtherRows(),keySelector).ToList();
        var actual=Rows().UnionBy(OtherRows(),keySelector).ToList();
        AssertSequenceEqual(expected,actual);
    }

}
