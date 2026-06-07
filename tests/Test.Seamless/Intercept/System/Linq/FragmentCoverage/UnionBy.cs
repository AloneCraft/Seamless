using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class UnionBy {
    [Fact,Trait("Category","Unit")]
    public static void UnionBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<UnionBy>(nameof(UnionBy_keySelectorImpl));
        var expected=SystemEnumerable.UnionBy(new[]{1,2,3},new[]{3,4,5},static v => v).ToList();
        var actual=UnionBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> UnionBy_keySelectorImpl(){
        return new[]{1,2,3}.UnionBy(new[]{3,4,5},static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void UnionBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<UnionBy>(nameof(UnionBy_comparerImpl));
        var expected=SystemEnumerable.UnionBy(new[]{1,2,3},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=UnionBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> UnionBy_comparerImpl(){
        return new[]{1,2,3}.UnionBy(new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void UnionBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<UnionBy>(nameof(UnionBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.UnionBy(new[]{1,2,3},new[]{3,4,5},static v => v).ToList();
        var actual=UnionBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> UnionBy_keySelector_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{3,4,5};
        return first.UnionBy(second,static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void UnionBy_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<UnionBy>(nameof(UnionBy_comparer_enumerableImpl));
        var expected=SystemEnumerable.UnionBy(new[]{1,2,3},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=UnionBy_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> UnionBy_comparer_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{3,4,5};
        return first.UnionBy(second,static v => v,EqualityComparer<int>.Default).ToList();
    }
}
