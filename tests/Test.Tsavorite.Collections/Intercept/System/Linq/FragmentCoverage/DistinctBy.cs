using System.Collections.Generic;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class DistinctBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(keySelectorImpl));
        var expected=SystemEnumerable.DistinctBy([1,2,2,3],static v => v).ToList();
        var actual=keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectorImpl(){
        return new[]{1,2,2,3}.DistinctBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(comparerImpl));
        var expected=SystemEnumerable.DistinctBy([1,2,2,3],static v => v,EqualityComparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{1,2,2,3}.DistinctBy(static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(keySelector_enumerableImpl));
        var expected=SystemEnumerable.DistinctBy([1,2,2,3],static v => v).ToList();
        var actual=keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,2,3];
        return src.DistinctBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(comparer_enumerableImpl));
        var expected=SystemEnumerable.DistinctBy([1,2,2,3],static v => v,EqualityComparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>src=[1,2,2,3];
        return src.DistinctBy(static v => v,EqualityComparer<int>.Default).ToList();
    }
}
