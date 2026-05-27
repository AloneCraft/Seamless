using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ThenByDescending:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.ThenByDescending(LinqChain1TestBase.Rows().OrderBy(static row => row.Key),static row => row.Name);
        var actual=LinqChain1TestBase.Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Lambda() => keySelector_LambdaImpl();

    private static void keySelector_LambdaImpl() {
        var expected=SystemEnumerable.ThenByDescending(LinqChain1TestBase.Rows().OrderBy(row => row.Key),row => row.Name);
        var actual=LinqChain1TestBase.Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Captured() => keySelector_CapturedImpl();

    private static void keySelector_CapturedImpl() {
        var suffix="";
        var expected=SystemEnumerable.ThenByDescending(LinqChain1TestBase.Rows().OrderBy(row => row.Key),row => row.Name+suffix);
        var actual=LinqChain1TestBase.Rows().OrderBy(row => row.Key).ThenByDescending(row => row.Name+suffix);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_DelegateSyntax() => keySelector_DelegateSyntaxImpl();

    private static void keySelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.ThenByDescending(LinqChain1TestBase.Rows().OrderBy(row => row.Key),delegate(Chain1Row row) { return row.Name; });
        var actual=LinqChain1TestBase.Rows().OrderBy(row => row.Key).ThenByDescending(delegate(Chain1Row row) { return row.Name; });
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_Variable() => keySelector_VariableImpl();

    private static void keySelector_VariableImpl() {
        Func<Chain1Row,string> keySelector=row => row.Name;
        var expected=SystemEnumerable.ThenByDescending(LinqChain1TestBase.Rows().OrderBy(row => row.Key),keySelector);
        var actual=LinqChain1TestBase.Rows().OrderBy(row => row.Key).ThenByDescending(keySelector);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() => keySelector_comparerImpl();

    private static void keySelector_comparerImpl() {
        var expected=SystemEnumerable.ThenByDescending(LinqChain1TestBase.Rows().OrderBy(static row => row.Key),static row => row.Name,StringComparer.Ordinal);
        var actual=LinqChain1TestBase.Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name,StringComparer.Ordinal);
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
