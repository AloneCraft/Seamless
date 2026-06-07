using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class CountBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<CountBy>(nameof(keySelectorImpl));
        var expected=SystemEnumerable.CountBy(new[]{1,1,2,2},static v => v).OrderBy(static kv => kv.Key).ToList();
        var actual=keySelectorImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> keySelectorImpl(){
        return new[]{1,1,2,2}.CountBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<CountBy>(nameof(keySelector_enumerableImpl));
        var expected=SystemEnumerable.CountBy(new[]{1,1,2,2},static v => v).OrderBy(static kv => kv.Key).ToList();
        var actual=keySelector_enumerableImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.CountBy(static v => v).ToList();
    }
}
