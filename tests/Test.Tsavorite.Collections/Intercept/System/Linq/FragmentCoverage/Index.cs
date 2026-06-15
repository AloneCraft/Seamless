using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Index {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.Index().ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> noArgImpl(){
        return new[]{1,2,3,4}.Index().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.Index().ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Index().ToList();
    }
}
