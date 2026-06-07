using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class OrderByDescending:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.OrderByDescending(LinqChain1TestBase.Rows(),static row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        LinqChain1TestBase.Rows().OrderByDescending(static row => row.Key).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.OrderByDescending(LinqChain1TestBase.Rows(),row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        LinqChain1TestBase.Rows().OrderByDescending(row => row.Key).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.OrderByDescending(LinqChain1TestBase.Rows(),row => row.Key+offset).ToList();
        var actual=new Consumer<Chain1Row>();
        LinqChain1TestBase.Rows().OrderByDescending(row => row.Key+offset).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.OrderByDescending(LinqChain1TestBase.Rows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=new Consumer<Chain1Row>();
        LinqChain1TestBase.Rows().OrderByDescending(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.OrderByDescending(LinqChain1TestBase.Rows(),keySelector).ToList();
        var actual=new Consumer<Chain1Row>();
        LinqChain1TestBase.Rows().OrderByDescending(keySelector).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() => keySelector_comparerImpl();

    private static void keySelector_comparerImpl() {
        var expected=SystemEnumerable.OrderByDescending(LinqChain1TestBase.Rows(),static row => row.Key,Comparer<int>.Default).ToList();
        var actual=new Consumer<Chain1Row>();
        LinqChain1TestBase.Rows().OrderByDescending(static row => row.Key,Comparer<int>.Default).Consume(actual);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual.Items);
    }

}
