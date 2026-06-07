using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class IntersectBy {
    [Fact,Trait("Category","Unit")]
    public static void IntersectBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(IntersectBy_keySelectorImpl));
        var expected=SystemEnumerable.IntersectBy(new[]{1,2,3,4},new[]{3,4,5},static v => v).ToList();
        var actual=IntersectBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_keySelectorImpl(){
        return new[]{1,2,3,4}.IntersectBy(new[]{3,4,5},static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void IntersectBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(IntersectBy_comparerImpl));
        var expected=SystemEnumerable.IntersectBy(new[]{1,2,3,4},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=IntersectBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_comparerImpl(){
        return new[]{1,2,3,4}.IntersectBy(new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void IntersectBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(IntersectBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.IntersectBy(new[]{1,2,3,4},new[]{3,4,5},static v => v).ToList();
        var actual=IntersectBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_keySelector_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.IntersectBy(second,static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void IntersectBy_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(IntersectBy_comparer_enumerableImpl));
        var expected=SystemEnumerable.IntersectBy(new[]{1,2,3,4},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=IntersectBy_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_comparer_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.IntersectBy(second,static v => v,EqualityComparer<int>.Default).ToList();
    }
}
