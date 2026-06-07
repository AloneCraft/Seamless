using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class TakeWhile {
    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<TakeWhile>(nameof(predicateImpl));
        var expected=SystemEnumerable.TakeWhile(new[]{1,2,3,4},static v => v<3).ToList();
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> predicateImpl(){
        return new[]{1,2,3,4}.TakeWhile(static v => v<3).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<TakeWhile>(nameof(predicate_enumerableImpl));
        var expected=SystemEnumerable.TakeWhile(new[]{1,2,3,4},static v => v<3).ToList();
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.TakeWhile(static v => v<3).ToList();
    }
}
