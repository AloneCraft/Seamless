using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Cast {
    [Fact,Trait("Category","Unit")]
    public static void Cast_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Cast>(nameof(Cast_noArgImpl));
        IEnumerable src=new object[]{1,2,3};
        var expected=SystemEnumerable.Cast<int>(src).ToList();
        var actual=Cast_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Cast_noArgImpl(){
        IEnumerable src=new object[]{1,2,3};
        return src.Cast<int>().ToList();
    }
}
