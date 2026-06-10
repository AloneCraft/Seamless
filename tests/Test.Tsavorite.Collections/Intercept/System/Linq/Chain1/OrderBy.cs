using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class OrderBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.OrderBy(Rows(),static row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.OrderBy(Rows(),row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.OrderBy(Rows(),row => row.Key+offset).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.OrderBy(Rows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.OrderBy(Rows(),keySelector).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Nested() => keySelector_NestedImpl();

    private static void keySelector_NestedImpl() {
        var expected=SystemEnumerable.OrderBy(Source(),p => SystemEnumerable.First(SystemEnumerable.OrderBy(Source(),q => q+p))).ToList();
        var actual=new Consumer<int>();
        Source().OrderBy(p => Source().OrderBy(q => q+p).First()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Query() => keySelector_QueryImpl();

    private static void keySelector_QueryImpl() {
        var expected=SystemEnumerable.OrderBy(Rows(),row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        (from row in Rows() orderby row.Key select row).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() => keySelector_comparerImpl();

    private static void keySelector_comparerImpl() {
        var expected=SystemEnumerable.OrderBy(Rows(),static row => row.Key,Comparer<int>.Default).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().OrderBy(static row => row.Key,Comparer<int>.Default).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
