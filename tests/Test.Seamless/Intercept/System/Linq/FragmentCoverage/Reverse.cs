using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Reverse {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(noArgImpl));
        var expected=SystemEnumerable.Reverse(new[]{1,2,3,4}).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,3,4}.Reverse().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.Reverse(new[]{1,2,3,4}).ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Reverse().ToList();
    }
}
