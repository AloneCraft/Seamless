using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Any {
    [Fact,Trait("Category","Unit")]
    public static void Any_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(Any_noArgImpl));
        var expected=SystemEnumerable.Any(new[]{1,2,3,4});
        var actual=Any_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static bool Any_noArgImpl(){
        return new[]{1,2,3,4}.Any();
    }

    [Fact,Trait("Category","Unit")]
    public static void Any_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(Any_predicateImpl));
        var expected=SystemEnumerable.Any(new[]{1,2,3,4},static v => v>2);
        var actual=Any_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static bool Any_predicateImpl(){
        return new[]{1,2,3,4}.Any(static v => v>2);
    }

    [Fact,Trait("Category","Unit")]
    public static void Any_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(Any_noArg_enumerableImpl));
        var expected=SystemEnumerable.Any(new[]{1,2,3,4});
        var actual=Any_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static bool Any_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Any();
    }

    [Fact,Trait("Category","Unit")]
    public static void Any_predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(Any_predicate_enumerableImpl));
        var expected=SystemEnumerable.Any(new[]{1,2,3,4},static v => v>2);
        var actual=Any_predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static bool Any_predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Any(static v => v>2);
    }
}
