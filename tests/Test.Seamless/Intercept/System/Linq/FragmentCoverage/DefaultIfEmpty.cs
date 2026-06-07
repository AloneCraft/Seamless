using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class DefaultIfEmpty {
    [Fact,Trait("Category","Unit")]
    public static void DefaultIfEmpty_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<DefaultIfEmpty>(nameof(DefaultIfEmpty_noArgImpl));
        var expected=SystemEnumerable.DefaultIfEmpty(new[]{1,2,3,4}).ToList();
        var actual=DefaultIfEmpty_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DefaultIfEmpty_noArgImpl(){
        return new[]{1,2,3,4}.DefaultIfEmpty().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void DefaultIfEmpty_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<DefaultIfEmpty>(nameof(DefaultIfEmpty_noArg_enumerableImpl));
        var expected=SystemEnumerable.DefaultIfEmpty(new[]{1,2,3,4}).ToList();
        var actual=DefaultIfEmpty_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DefaultIfEmpty_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.DefaultIfEmpty().ToList();
    }
}
