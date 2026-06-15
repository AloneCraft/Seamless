using System.Collections.Generic;
using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Any {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.Any();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static bool noArgImpl(){
        return new[]{1,2,3,4}.Any();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(predicateImpl));
        var expected=new[]{1,2,3,4}.Any(static v => v>2);
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static bool predicateImpl(){
        return new[]{1,2,3,4}.Any(static v => v>2);
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.Any();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static bool noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Any();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(predicate_enumerableImpl));
        var expected=new[]{1,2,3,4}.Any(static v => v>2);
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static bool predicate_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Any(static v => v>2);
    }
}
