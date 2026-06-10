using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class GroupJoin {
    [Fact,Trait("Category","Unit")]
    public static void keySelectors_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupJoin>(nameof(keySelectors_resultSelectorImpl));
        var expected=SystemEnumerable.GroupJoin([1,2,3],[2,3,4],static o => o,static i => i,static (o,ig) => o).ToList();
        var actual=keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectors_resultSelectorImpl(){
        return new[]{1,2,3}.GroupJoin([2,3,4],static o => o,static i => i,static (o,ig) => o).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelectors_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupJoin>(nameof(keySelectors_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupJoin([1,2,3],[2,3,4],static o => o,static i => i,static (o,ig) => o).ToList();
        var actual=keySelectors_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectors_resultSelector_enumerableImpl(){
        IEnumerable<int>outer=[1,2,3];
        IEnumerable<int>inner=[2,3,4];
        return outer.GroupJoin(inner,static o => o,static i => i,static (o,ig) => o).ToList();
    }
}
