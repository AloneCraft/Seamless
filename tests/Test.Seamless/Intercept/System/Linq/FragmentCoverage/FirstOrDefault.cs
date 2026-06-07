using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class FirstOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void FirstOrDefault_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<FirstOrDefault>(nameof(FirstOrDefault_noArgImpl));
        var expected=SystemEnumerable.FirstOrDefault(new[]{1,2,3,4});
        var actual=FirstOrDefault_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int FirstOrDefault_noArgImpl(){
        return new[]{1,2,3,4}.FirstOrDefault();
    }

    [Fact,Trait("Category","Unit")]
    public static void FirstOrDefault_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<FirstOrDefault>(nameof(FirstOrDefault_noArg_enumerableImpl));
        var expected=SystemEnumerable.FirstOrDefault(new[]{1,2,3,4});
        var actual=FirstOrDefault_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int FirstOrDefault_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.FirstOrDefault();
    }
}
