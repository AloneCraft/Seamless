using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Index {
    [Fact,Trait("Category","Unit")]
    public static void Index_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(Index_noArgImpl));
        var expected=SystemEnumerable.Index(new[]{1,2,3,4}).ToList();
        var actual=Index_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> Index_noArgImpl(){
        return new[]{1,2,3,4}.Index().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Index_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(Index_noArg_enumerableImpl));
        var expected=SystemEnumerable.Index(new[]{1,2,3,4}).ToList();
        var actual=Index_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> Index_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Index().ToList();
    }
}
