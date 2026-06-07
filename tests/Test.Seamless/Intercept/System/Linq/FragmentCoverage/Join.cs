using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Join {
    [Fact,Trait("Category","Unit")]
    public static void Join_keySelectors_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Join>(nameof(Join_keySelectors_resultSelectorImpl));
        var expected=SystemEnumerable.Join(new[]{1,2,3},new[]{2,3,4},static o => o,static i => i,static (o,i) => o+i).ToList();
        var actual=Join_keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Join_keySelectors_resultSelectorImpl(){
        return new[]{1,2,3}.Join(new[]{2,3,4},static o => o,static i => i,static (o,i) => o+i).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Join_keySelectors_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Join>(nameof(Join_keySelectors_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.Join(new[]{1,2,3},new[]{2,3,4},static o => o,static i => i,static (o,i) => o+i).ToList();
        var actual=Join_keySelectors_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Join_keySelectors_resultSelector_enumerableImpl(){
        IEnumerable<int>outer=new[]{1,2,3};
        IEnumerable<int>inner=new[]{2,3,4};
        return outer.Join(inner,static o => o,static i => i,static (o,i) => o+i).ToList();
    }
}
