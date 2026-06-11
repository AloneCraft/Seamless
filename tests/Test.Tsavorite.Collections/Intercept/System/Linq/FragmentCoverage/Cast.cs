using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Cast {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Cast>(nameof(noArgImpl));
        IEnumerable src=new object[]{1,2,3};
        var expected=SystemEnumerable.Cast<int>(src).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        IEnumerable src=new object[]{1,2,3};
        return src.Cast<int>().ToList();
    }
}
