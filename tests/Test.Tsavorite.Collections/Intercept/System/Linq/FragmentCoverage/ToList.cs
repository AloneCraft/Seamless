using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class ToList {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToList>(nameof(noArgImpl));
        var expected=SystemEnumerable.ToList([1,2,3,4]);
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,3,4}.ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToList>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.ToList([1,2,3,4]);
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToList();
    }
}
