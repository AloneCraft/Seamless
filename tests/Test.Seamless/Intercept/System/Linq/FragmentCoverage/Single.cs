using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Single {
    [Fact,Trait("Category","Unit")]
    public static void Single_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Single>(nameof(Single_noArgImpl));
        var expected=SystemEnumerable.Single(new[]{42});
        var actual=Single_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Single_noArgImpl(){
        return new[]{42}.Single();
    }

    [Fact,Trait("Category","Unit")]
    public static void Single_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Single>(nameof(Single_noArg_enumerableImpl));
        var expected=SystemEnumerable.Single(new[]{42});
        var actual=Single_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Single_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{42};
        return src.Single();
    }
}
