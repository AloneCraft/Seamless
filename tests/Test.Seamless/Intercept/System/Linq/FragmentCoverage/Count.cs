using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Count {
    [Fact,Trait("Category","Unit")]
    public static void Count_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(Count_noArgImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4});
        var actual=Count_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Count_noArgImpl(){
        return new[]{1,2,3,4}.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void Count_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(Count_predicateImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4},static v => v>2);
        var actual=Count_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static int Count_predicateImpl(){
        return new[]{1,2,3,4}.Count(static v => v>2);
    }

    [Fact,Trait("Category","Unit")]
    public static void Count_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(Count_noArg_enumerableImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4});
        var actual=Count_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Count_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void Count_predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(Count_predicate_enumerableImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4},static v => v>2);
        var actual=Count_predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Count_predicate_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Count(static v => v>2);
    }
}
