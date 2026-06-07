using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class ToArray {
    [Fact,Trait("Category","Unit")]
    public static void ToArray_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToArray>(nameof(ToArray_noArgImpl));
        var expected=SystemEnumerable.ToArray(new[]{1,2,3,4});
        var actual=ToArray_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int[] ToArray_noArgImpl(){
        return new[]{1,2,3,4}.ToArray();
    }

    [Fact,Trait("Category","Unit")]
    public static void ToArray_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToArray>(nameof(ToArray_noArg_enumerableImpl));
        var expected=SystemEnumerable.ToArray(new[]{1,2,3,4});
        var actual=ToArray_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int[] ToArray_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.ToArray();
    }
}
