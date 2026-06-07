using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class SkipLast {
    [Fact,Trait("Category","Unit")]
    public static void count(){
        GeneratedInterceptorAssert.ContainsMethod<SkipLast>(nameof(countImpl));
        var expected=SystemEnumerable.SkipLast(new[]{1,2,3,4},1).ToList();
        var actual=countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> countImpl(){
        return new[]{1,2,3,4}.SkipLast(1).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SkipLast>(nameof(count_enumerableImpl));
        var expected=SystemEnumerable.SkipLast(new[]{1,2,3,4},1).ToList();
        var actual=count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> count_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SkipLast(1).ToList();
    }
}
