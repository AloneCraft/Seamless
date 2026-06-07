using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class DistinctBy {
    [Fact,Trait("Category","Unit")]
    public static void DistinctBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(DistinctBy_keySelectorImpl));
        var expected=SystemEnumerable.DistinctBy(new[]{1,2,2,3},static v => v).ToList();
        var actual=DistinctBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DistinctBy_keySelectorImpl(){
        return new[]{1,2,2,3}.DistinctBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void DistinctBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(DistinctBy_comparerImpl));
        var expected=SystemEnumerable.DistinctBy(new[]{1,2,2,3},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=DistinctBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DistinctBy_comparerImpl(){
        return new[]{1,2,2,3}.DistinctBy(static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void DistinctBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(DistinctBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.DistinctBy(new[]{1,2,2,3},static v => v).ToList();
        var actual=DistinctBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DistinctBy_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,2,3};
        return src.DistinctBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void DistinctBy_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(DistinctBy_comparer_enumerableImpl));
        var expected=SystemEnumerable.DistinctBy(new[]{1,2,2,3},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=DistinctBy_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DistinctBy_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,2,3};
        return src.DistinctBy(static v => v,EqualityComparer<int>.Default).ToList();
    }
}
