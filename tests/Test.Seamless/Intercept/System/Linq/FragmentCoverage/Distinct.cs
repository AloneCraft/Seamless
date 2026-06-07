using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Distinct {
    [Fact,Trait("Category","Unit")]
    public static void Distinct_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(Distinct_noArgImpl));
        var expected=SystemEnumerable.Distinct(new[]{1,2,2,3}).ToList();
        var actual=Distinct_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Distinct_noArgImpl(){
        return new[]{1,2,2,3}.Distinct().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Distinct_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(Distinct_comparerImpl));
        var expected=SystemEnumerable.Distinct(new[]{1,2,2,3},EqualityComparer<int>.Default).ToList();
        var actual=Distinct_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Distinct_comparerImpl(){
        return new[]{1,2,2,3}.Distinct(EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Distinct_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(Distinct_noArg_enumerableImpl));
        var expected=SystemEnumerable.Distinct(new[]{1,2,2,3}).ToList();
        var actual=Distinct_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Distinct_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,2,3};
        return src.Distinct().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Distinct_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(Distinct_comparer_enumerableImpl));
        var expected=SystemEnumerable.Distinct(new[]{1,2,2,3},EqualityComparer<int>.Default).ToList();
        var actual=Distinct_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Distinct_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,2,3};
        return src.Distinct(EqualityComparer<int>.Default).ToList();
    }
}
