using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Index {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(noArgImpl));
        var expected=SystemEnumerable.Index(new[]{1,2,3,4}).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> noArgImpl(){
        return new[]{1,2,3,4}.Index().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.Index(new[]{1,2,3,4}).ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Index().ToList();
    }
}
