using System.Collections.Generic;
using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Count {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.Count();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{1,2,3,4}.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(predicateImpl));
        var expected=new[]{1,2,3,4}.Count(static v => v>2);
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static int predicateImpl(){
        return new[]{1,2,3,4}.Count(static v => v>2);
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.Count();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(predicate_enumerableImpl));
        var expected=new[]{1,2,3,4}.Count(static v => v>2);
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int predicate_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Count(static v => v>2);
    }
}
