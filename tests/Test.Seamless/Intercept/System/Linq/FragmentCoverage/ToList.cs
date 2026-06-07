using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class ToList {
    [Fact,Trait("Category","Unit")]
    public static void ToList_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToList>(nameof(ToList_noArgImpl));
        var expected=SystemEnumerable.ToList(new[]{1,2,3,4});
        var actual=ToList_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ToList_noArgImpl(){
        return new[]{1,2,3,4}.ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ToList_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToList>(nameof(ToList_noArg_enumerableImpl));
        var expected=SystemEnumerable.ToList(new[]{1,2,3,4});
        var actual=ToList_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ToList_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.ToList();
    }
}
