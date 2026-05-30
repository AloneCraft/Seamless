using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class GroupBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key).ToList();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_Static() => elementSelector_StaticImpl();

    private static void elementSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key,static row => row.Value).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key,static row => row.Value).ToList();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void resultSelector_Static() => resultSelector_StaticImpl();

    private static void resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key,static (key,group) => group.Sum(row => row.Value)).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value)).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_resultSelector_Static() => elementSelector_resultSelector_StaticImpl();

    private static void elementSelector_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum()).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),row => row.Key).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(row => row.Key).ToList();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),row => row.Key+offset).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(row => row.Key+offset).ToList();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),delegate(Chain1Row row) { return row.Key; }).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(delegate(Chain1Row row) { return row.Key; }).ToList();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),keySelector).ToList();
        var actual=LinqChain1TestBase.Rows().GroupBy(keySelector).ToList();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

}
