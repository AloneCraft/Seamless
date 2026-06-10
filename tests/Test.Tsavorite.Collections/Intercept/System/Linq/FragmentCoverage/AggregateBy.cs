using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class AggregateBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<AggregateBy>(nameof(keySelectorImpl));
        var expected=SystemEnumerable.AggregateBy([1,2,3,4],static v => v%2,0,static (a,v) => a+v).OrderBy(static kv => kv.Key).ToList();
        var actual=keySelectorImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> keySelectorImpl(){
        return new[]{1,2,3,4}.AggregateBy(static v => v%2,0,static (a,v) => a+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<AggregateBy>(nameof(keySelector_enumerableImpl));
        var expected=SystemEnumerable.AggregateBy([1,2,3,4],static v => v%2,0,static (a,v) => a+v).OrderBy(static kv => kv.Key).ToList();
        var actual=keySelector_enumerableImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> keySelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.AggregateBy(static v => v%2,0,static (a,v) => a+v).ToList();
    }
}
