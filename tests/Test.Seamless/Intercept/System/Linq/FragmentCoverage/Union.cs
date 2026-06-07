using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Union {
    [Fact,Trait("Category","Unit")]
    public static void Union_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(Union_noArgImpl));
        var expected=SystemEnumerable.Union(new[]{1,2,3},new[]{3,4,5}).ToList();
        var actual=Union_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_noArgImpl(){
        return new[]{1,2,3}.Union(new[]{3,4,5}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Union_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(Union_comparerImpl));
        var expected=SystemEnumerable.Union(new[]{1,2,3},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Union_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_comparerImpl(){
        return new[]{1,2,3}.Union(new[]{3,4,5},EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Union_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(Union_noArg_enumerableImpl));
        var expected=SystemEnumerable.Union(new[]{1,2,3},new[]{3,4,5}).ToList();
        var actual=Union_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_noArg_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{3,4,5};
        return first.Union(second).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Union_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(Union_comparer_enumerableImpl));
        var expected=SystemEnumerable.Union(new[]{1,2,3},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Union_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_comparer_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{3,4,5};
        return first.Union(second,EqualityComparer<int>.Default).ToList();
    }
}
