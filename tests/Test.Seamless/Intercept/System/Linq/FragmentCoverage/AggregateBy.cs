using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class AggregateBy {
    [Fact,Trait("Category","Unit")]
    public static void AggregateBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<AggregateBy>(nameof(AggregateBy_keySelectorImpl));
        var expected=SystemEnumerable.AggregateBy(new[]{1,2,3,4},static v => v%2,0,static (a,v) => a+v).OrderBy(static kv => kv.Key).ToList();
        var actual=AggregateBy_keySelectorImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> AggregateBy_keySelectorImpl(){
        return new[]{1,2,3,4}.AggregateBy(static v => v%2,0,static (a,v) => a+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void AggregateBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<AggregateBy>(nameof(AggregateBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.AggregateBy(new[]{1,2,3,4},static v => v%2,0,static (a,v) => a+v).OrderBy(static kv => kv.Key).ToList();
        var actual=AggregateBy_keySelector_enumerableImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> AggregateBy_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.AggregateBy(static v => v%2,0,static (a,v) => a+v).ToList();
    }
}
