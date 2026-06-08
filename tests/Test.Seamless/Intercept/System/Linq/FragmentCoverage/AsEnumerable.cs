using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class AsEnumerable {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<AsEnumerable>(nameof(noArgImpl));
        var expected=SystemEnumerable.AsEnumerable([1,2,3,4]).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,3,4}.AsEnumerable().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<AsEnumerable>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.AsEnumerable([1,2,3,4]).ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.AsEnumerable().ToList();
    }
}
