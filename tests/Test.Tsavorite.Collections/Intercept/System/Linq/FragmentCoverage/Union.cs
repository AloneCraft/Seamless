using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Union {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(noArgImpl));
        var expected=new[]{1,2,3}.Union([3,4,5]).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,3}.Union([3,4,5]).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(comparerImpl));
        var expected=new[]{1,2,3}.Union([3,4,5],EqualityComparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{1,2,3}.Union([3,4,5],EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3}.Union([3,4,5]).ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>first=[1,2,3];
        IEnumerable<int>second=[3,4,5];
        return first.Union(second).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(comparer_enumerableImpl));
        var expected=new[]{1,2,3}.Union([3,4,5],EqualityComparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>first=[1,2,3];
        IEnumerable<int>second=[3,4,5];
        return first.Union(second,EqualityComparer<int>.Default).ToList();
    }
}
