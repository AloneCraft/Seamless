using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class Except {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(noArgImpl));
        var expected=SystemEnumerable.Except([1,2,3,4],[3,4,5]).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,3,4}.Except([3,4,5]).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(comparerImpl));
        var expected=SystemEnumerable.Except([1,2,3,4],[3,4,5],EqualityComparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{1,2,3,4}.Except([3,4,5],EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.Except([1,2,3,4],[3,4,5]).ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>first=[1,2,3,4];
        IEnumerable<int>second=[3,4,5];
        return first.Except(second).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(comparer_enumerableImpl));
        var expected=SystemEnumerable.Except([1,2,3,4],[3,4,5],EqualityComparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>first=[1,2,3,4];
        IEnumerable<int>second=[3,4,5];
        return first.Except(second,EqualityComparer<int>.Default).ToList();
    }
}
