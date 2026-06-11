using System.Collections;
using System.Collections.Generic;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class OfType {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<OfType>(nameof(noArgImpl));
        IEnumerable src=new object?[]{1,"x",2,null};
        var expected=SystemEnumerable.OfType<int>(src).ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        IEnumerable src=new object?[]{1,"x",2,null};
        return src.OfType<int>().ToList();
    }
}
