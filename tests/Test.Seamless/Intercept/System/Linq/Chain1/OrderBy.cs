using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class OrderBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.OrderBy(LinqChain1TestBase.Rows(),static row => row.Key);
        var actual=LinqChain1TestBase.Rows().OrderBy(static row => row.Key);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.OrderBy(LinqChain1TestBase.Rows(),row => row.Key);
        var actual=LinqChain1TestBase.Rows().OrderBy(row => row.Key);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.OrderBy(LinqChain1TestBase.Rows(),row => row.Key+offset);
        var actual=LinqChain1TestBase.Rows().OrderBy(row => row.Key+offset);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.OrderBy(LinqChain1TestBase.Rows(),delegate(Chain1Row row) { return row.Key; });
        var actual=LinqChain1TestBase.Rows().OrderBy(delegate(Chain1Row row) { return row.Key; });
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.OrderBy(LinqChain1TestBase.Rows(),keySelector);
        var actual=LinqChain1TestBase.Rows().OrderBy(keySelector);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() => keySelector_comparerImpl();

    private static void keySelector_comparerImpl() {
        var expected=SystemEnumerable.OrderBy(LinqChain1TestBase.Rows(),static row => row.Key,Comparer<int>.Default);
        var actual=LinqChain1TestBase.Rows().OrderBy(static row => row.Key,Comparer<int>.Default);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
