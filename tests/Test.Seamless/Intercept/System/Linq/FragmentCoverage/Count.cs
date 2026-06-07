using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Count {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(noArgImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4});
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{1,2,3,4}.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(predicateImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4},static v => v>2);
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static int predicateImpl(){
        return new[]{1,2,3,4}.Count(static v => v>2);
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4});
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(predicate_enumerableImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4},static v => v>2);
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Count(static v => v>2);
    }
}
