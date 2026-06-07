using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Intersect {
    [Fact,Trait("Category","Unit")]
    public static void Intersect_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Intersect>(nameof(Intersect_noArgImpl));
        var expected=SystemEnumerable.Intersect(new[]{1,2,3,4},new[]{3,4,5}).ToList();
        var actual=Intersect_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Intersect_noArgImpl(){
        return new[]{1,2,3,4}.Intersect(new[]{3,4,5}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Intersect_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Intersect>(nameof(Intersect_comparerImpl));
        var expected=SystemEnumerable.Intersect(new[]{1,2,3,4},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Intersect_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Intersect_comparerImpl(){
        return new[]{1,2,3,4}.Intersect(new[]{3,4,5},EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Intersect_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Intersect>(nameof(Intersect_noArg_enumerableImpl));
        var expected=SystemEnumerable.Intersect(new[]{1,2,3,4},new[]{3,4,5}).ToList();
        var actual=Intersect_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Intersect_noArg_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.Intersect(second).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Intersect_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Intersect>(nameof(Intersect_comparer_enumerableImpl));
        var expected=SystemEnumerable.Intersect(new[]{1,2,3,4},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Intersect_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Intersect_comparer_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.Intersect(second,EqualityComparer<int>.Default).ToList();
    }
}
