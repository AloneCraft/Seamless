using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Where {
    [Fact,Trait("Category","Unit")]
    public static void Where_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(Where_predicateImpl));
        var expected=SystemEnumerable.Where(new[]{1,2,3,4},static v => v>2).ToList();
        var actual=Where_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_predicateImpl(){
        return new[]{1,2,3,4}.Where(static v => v>2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Where_indexPredicate(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(Where_indexPredicateImpl));
        var expected=SystemEnumerable.Where(new[]{1,2,3,4},static (v,i) => i>1).ToList();
        var actual=Where_indexPredicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_indexPredicateImpl(){
        return new[]{1,2,3,4}.Where(static (v,i) => i>1).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Where_predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(Where_predicate_enumerableImpl));
        var expected=SystemEnumerable.Where(new[]{1,2,3,4},static v => v>2).ToList();
        var actual=Where_predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Where(static v => v>2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Where_indexPredicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(Where_indexPredicate_enumerableImpl));
        var expected=SystemEnumerable.Where(new[]{1,2,3,4},static (v,i) => i>1).ToList();
        var actual=Where_indexPredicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_indexPredicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Where(static (v,i) => i>1).ToList();
    }
}
