using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class ToLookup:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void keySelector_Static() => keySelector_StaticImpl();

    private static void keySelector_StaticImpl() {
        var expected=SystemEnumerable.ToLookup(Rows(),static row => row.Key);
        var actual=Rows().ToLookup(static row => row.Key);
        AssertLookupEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_elementSelector_Static() => keySelector_elementSelector_StaticImpl();

    private static void keySelector_elementSelector_StaticImpl() {
        var expected=SystemEnumerable.ToLookup(Rows(),static row => row.Key,static row => row.Value);
        var actual=Rows().ToLookup(static row => row.Key,static row => row.Value);
        AssertLookupEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_comparer() => keySelector_comparerImpl();

    private static void keySelector_comparerImpl() {
        var expected=SystemEnumerable.ToLookup(Rows(),static row => row.Key,EqualityComparer<int>.Default);
        var actual=Rows().ToLookup(static row => row.Key,EqualityComparer<int>.Default);
        AssertLookupEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_elementSelector_comparer() => keySelector_elementSelector_comparerImpl();

    private static void keySelector_elementSelector_comparerImpl() {
        var expected=SystemEnumerable.ToLookup(Rows(),static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        var actual=Rows().ToLookup(static row => row.Key,static row => row.Value,EqualityComparer<int>.Default);
        AssertLookupEqual(expected,actual);
    }

}
