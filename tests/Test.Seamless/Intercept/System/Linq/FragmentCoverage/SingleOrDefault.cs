using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class SingleOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void SingleOrDefault_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<SingleOrDefault>(nameof(SingleOrDefault_noArgImpl));
        var expected=SystemEnumerable.SingleOrDefault(new[]{42});
        var actual=SingleOrDefault_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int SingleOrDefault_noArgImpl(){
        return new[]{42}.SingleOrDefault();
    }

    [Fact,Trait("Category","Unit")]
    public static void SingleOrDefault_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SingleOrDefault>(nameof(SingleOrDefault_noArg_enumerableImpl));
        var expected=SystemEnumerable.SingleOrDefault(new[]{42});
        var actual=SingleOrDefault_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int SingleOrDefault_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{42};
        return src.SingleOrDefault();
    }
}
