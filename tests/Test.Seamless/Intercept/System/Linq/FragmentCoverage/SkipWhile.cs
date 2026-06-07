using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class SkipWhile {
    [Fact,Trait("Category","Unit")]
    public static void SkipWhile_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<SkipWhile>(nameof(SkipWhile_predicateImpl));
        var expected=SystemEnumerable.SkipWhile(new[]{1,2,3,4},static v => v<3).ToList();
        var actual=SkipWhile_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SkipWhile_predicateImpl(){
        return new[]{1,2,3,4}.SkipWhile(static v => v<3).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SkipWhile_predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SkipWhile>(nameof(SkipWhile_predicate_enumerableImpl));
        var expected=SystemEnumerable.SkipWhile(new[]{1,2,3,4},static v => v<3).ToList();
        var actual=SkipWhile_predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SkipWhile_predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SkipWhile(static v => v<3).ToList();
    }
}
