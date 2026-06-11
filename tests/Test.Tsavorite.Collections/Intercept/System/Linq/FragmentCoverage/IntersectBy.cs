using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class IntersectBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(keySelectorImpl));
        var expected=SystemEnumerable.IntersectBy([1,2,3,4],[3,4,5],static v => v).ToList();
        var actual=keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectorImpl(){
        return new[]{1,2,3,4}.IntersectBy([3,4,5],static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(comparerImpl));
        var expected=SystemEnumerable.IntersectBy([1,2,3,4],[3,4,5],static v => v,EqualityComparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{1,2,3,4}.IntersectBy([3,4,5],static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(keySelector_enumerableImpl));
        var expected=SystemEnumerable.IntersectBy([1,2,3,4],[3,4,5],static v => v).ToList();
        var actual=keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_enumerableImpl(){
        IEnumerable<int>first=[1,2,3,4];
        IEnumerable<int>second=[3,4,5];
        return first.IntersectBy(second,static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(comparer_enumerableImpl));
        var expected=SystemEnumerable.IntersectBy([1,2,3,4],[3,4,5],static v => v,EqualityComparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>first=[1,2,3,4];
        IEnumerable<int>second=[3,4,5];
        return first.IntersectBy(second,static v => v,EqualityComparer<int>.Default).ToList();
    }
}
