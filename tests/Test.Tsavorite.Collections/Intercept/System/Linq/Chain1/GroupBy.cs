using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class GroupBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key).ToList();
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(static row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_Static() => elementSelector_StaticImpl();

    private static void elementSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key,static row => row.Value).ToList();
        var actual=new Consumer<IGrouping<int,int>>();
        Rows().GroupBy(static row => row.Key,static row => row.Value).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void resultSelector_Static() => resultSelector_StaticImpl();

    private static void resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key,static (key,group) => group.Sum(row => row.Value)).ToList();
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_resultSelector_Static() => elementSelector_resultSelector_StaticImpl();

    private static void elementSelector_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).ToList();
        var actual=new Consumer<int>();
        Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Nested() => keySelector_NestedImpl();

    private static void keySelector_NestedImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.GroupBy(Source(),p => SystemEnumerable.Count(SystemEnumerable.GroupBy(Source(),q => q%p))),group => group.Key).ToList();
        var actual=new Consumer<int>();
        Source().GroupBy(p => Source().GroupBy(q => q%p).Count()).Select(group => group.Key).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Query() => keySelector_QueryImpl();

    private static void keySelector_QueryImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key).ToList();
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        (from row in Rows() group row by row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key).ToList();
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key+offset).ToList();
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(row => row.Key+offset).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.GroupBy(Rows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.GroupBy(Rows(),keySelector).ToList();
        var actual=new Consumer<IGrouping<int,Chain1Row>>();
        Rows().GroupBy(keySelector).Consume(actual);
        AssertGroupSequenceEqual(expected,actual.Items);
    }

}
