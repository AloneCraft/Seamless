using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class All {
    [Fact,Trait("Category","Unit")]
    public static void All_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<All>(nameof(All_predicateImpl));
        var expected=SystemEnumerable.All(new[]{1,2,3,4},static v => v>0);
        var actual=All_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static bool All_predicateImpl(){
        return new[]{1,2,3,4}.All(static v => v>0);
    }

    [Fact,Trait("Category","Unit")]
    public static void All_predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<All>(nameof(All_predicate_enumerableImpl));
        var expected=SystemEnumerable.All(new[]{1,2,3,4},static v => v>0);
        var actual=All_predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static bool All_predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.All(static v => v>0);
    }
}
