using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class AsEnumerable {
    [Fact,Trait("Category","Unit")]
    public static void AsEnumerable_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<AsEnumerable>(nameof(AsEnumerable_noArgImpl));
        var expected=SystemEnumerable.AsEnumerable(new[]{1,2,3,4}).ToList();
        var actual=AsEnumerable_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> AsEnumerable_noArgImpl(){
        return new[]{1,2,3,4}.AsEnumerable().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void AsEnumerable_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<AsEnumerable>(nameof(AsEnumerable_noArg_enumerableImpl));
        var expected=SystemEnumerable.AsEnumerable(new[]{1,2,3,4}).ToList();
        var actual=AsEnumerable_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> AsEnumerable_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.AsEnumerable().ToList();
    }
}
