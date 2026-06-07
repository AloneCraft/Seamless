using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class OfType {
    [Fact,Trait("Category","Unit")]
    public static void OfType_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<OfType>(nameof(OfType_noArgImpl));
        IEnumerable src=new object?[]{1,"x",2,null};
        var expected=SystemEnumerable.OfType<int>(src).ToList();
        var actual=OfType_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OfType_noArgImpl(){
        IEnumerable src=new object?[]{1,"x",2,null};
        return src.OfType<int>().ToList();
    }
}
