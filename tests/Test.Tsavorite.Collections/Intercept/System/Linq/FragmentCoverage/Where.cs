using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class Where {
    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(predicateImpl));
        var expected=SystemEnumerable.Where([1,2,3,4],static v => v>2).ToList();
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> predicateImpl(){
        return new[]{1,2,3,4}.Where(static v => v>2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void indexPredicate(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(indexPredicateImpl));
        var expected=SystemEnumerable.Where([1,2,3,4],static (v,i) => i>1).ToList();
        var actual=indexPredicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexPredicateImpl(){
        return new[]{1,2,3,4}.Where(static (v,i) => i>1).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(predicate_enumerableImpl));
        var expected=SystemEnumerable.Where([1,2,3,4],static v => v>2).ToList();
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> predicate_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Where(static v => v>2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void indexPredicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(indexPredicate_enumerableImpl));
        var expected=SystemEnumerable.Where([1,2,3,4],static (v,i) => i>1).ToList();
        var actual=indexPredicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexPredicate_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Where(static (v,i) => i>1).ToList();
    }
}
