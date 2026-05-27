using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class GroupBy:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key);
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key);
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_Static() => elementSelector_StaticImpl();

    private static void elementSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key,static row => row.Value);
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key,static row => row.Value);
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void resultSelector_Static() => resultSelector_StaticImpl();

    private static void resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key,static (key,group) => group.Sum(row => row.Value));
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key,static (key,group) => group.Sum(row => row.Value));
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void elementSelector_resultSelector_Static() => elementSelector_resultSelector_StaticImpl();

    private static void elementSelector_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),static row => row.Key,static row => row.Value,static (key,group) => group.Sum());
        var actual=LinqChain1TestBase.Rows().GroupBy(static row => row.Key,static row => row.Value,static (key,group) => group.Sum());
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),row => row.Key);
        var actual=LinqChain1TestBase.Rows().GroupBy(row => row.Key);
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),row => row.Key+offset);
        var actual=LinqChain1TestBase.Rows().GroupBy(row => row.Key+offset);
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),delegate(Chain1Row row) { return row.Key; });
        var actual=LinqChain1TestBase.Rows().GroupBy(delegate(Chain1Row row) { return row.Key; });
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.GroupBy(LinqChain1TestBase.Rows(),keySelector);
        var actual=LinqChain1TestBase.Rows().GroupBy(keySelector);
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }

}
