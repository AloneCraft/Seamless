using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class ExceptBy {
    [Fact,Trait("Category","Unit")]
    public static void ExceptBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(ExceptBy_keySelectorImpl));
        var expected=SystemEnumerable.ExceptBy(new[]{1,2,3,4},new[]{3,4,5},static v => v).ToList();
        var actual=ExceptBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ExceptBy_keySelectorImpl(){
        return new[]{1,2,3,4}.ExceptBy(new[]{3,4,5},static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ExceptBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(ExceptBy_comparerImpl));
        var expected=SystemEnumerable.ExceptBy(new[]{1,2,3,4},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=ExceptBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ExceptBy_comparerImpl(){
        return new[]{1,2,3,4}.ExceptBy(new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ExceptBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(ExceptBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.ExceptBy(new[]{1,2,3,4},new[]{3,4,5},static v => v).ToList();
        var actual=ExceptBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ExceptBy_keySelector_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.ExceptBy(second,static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ExceptBy_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(ExceptBy_comparer_enumerableImpl));
        var expected=SystemEnumerable.ExceptBy(new[]{1,2,3,4},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=ExceptBy_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ExceptBy_comparer_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.ExceptBy(second,static v => v,EqualityComparer<int>.Default).ToList();
    }
}
