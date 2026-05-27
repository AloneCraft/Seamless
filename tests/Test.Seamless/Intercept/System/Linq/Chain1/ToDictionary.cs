using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ToDictionary:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.ToDictionary(LinqChain1TestBase.Rows().Take(3),static row => row.Key);
        var actual=LinqChain1TestBase.Rows().Take(3).ToDictionary(static row => row.Key);
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_elementSelector_Static() => keySelector_elementSelector_StaticImpl();

    private static void keySelector_elementSelector_StaticImpl() {
        var expected=SystemEnumerable.ToDictionary(LinqChain1TestBase.Rows().Take(3),static row => row.Key,static row => row.Value);
        var actual=LinqChain1TestBase.Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value);
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() => keySelector_comparerImpl();

    private static void keySelector_comparerImpl() {
        var expected=SystemEnumerable.ToDictionary(LinqChain1TestBase.Rows().Take(3),static row => row.Key,EqualityComparer<int>.Default);
        var actual=LinqChain1TestBase.Rows().Take(3).ToDictionary(static row => row.Key,EqualityComparer<int>.Default);
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_elementSelector_comparer() => keySelector_elementSelector_comparerImpl();

    private static void keySelector_elementSelector_comparerImpl() {
        var expected=SystemEnumerable.ToDictionary(LinqChain1TestBase.Rows().Take(3),static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        var actual=LinqChain1TestBase.Rows().Take(3).ToDictionary(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }

}
