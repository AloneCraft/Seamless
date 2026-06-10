using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class DistinctBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.DistinctBy(Rows(),static row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(static row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Nested() => keySelector_NestedImpl();

    private static void keySelector_NestedImpl() {
        var expected=SystemEnumerable.DistinctBy(Source(),p => SystemEnumerable.Count(SystemEnumerable.DistinctBy(Source(),q => q%p))).ToList();
        var actual=new Consumer<int>();
        Source().DistinctBy(p => Source().DistinctBy(q => q%p).Count()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.DistinctBy(Rows(),row => row.Key).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.DistinctBy(Rows(),row => row.Key+offset).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(row => row.Key+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.DistinctBy(Rows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.DistinctBy(Rows(),keySelector).ToList();
        var actual=new Consumer<Chain1Row>();
        Rows().DistinctBy(keySelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
