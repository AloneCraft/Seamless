using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Reverse {
    [Fact,Trait("Category","Unit")]
    public static void Reverse_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(Reverse_noArgImpl));
        var expected=SystemEnumerable.Reverse(new[]{1,2,3,4}).ToList();
        var actual=Reverse_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Reverse_noArgImpl(){
        return new[]{1,2,3,4}.Reverse().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Reverse_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(Reverse_noArg_enumerableImpl));
        var expected=SystemEnumerable.Reverse(new[]{1,2,3,4}).ToList();
        var actual=Reverse_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Reverse_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Reverse().ToList();
    }
}
