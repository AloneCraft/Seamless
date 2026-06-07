using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Skip {
    [Fact,Trait("Category","Unit")]
    public static void Skip_count(){
        GeneratedInterceptorAssert.ContainsMethod<Skip>(nameof(Skip_countImpl));
        var expected=SystemEnumerable.Skip(new[]{1,2,3,4},1).ToList();
        var actual=Skip_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Skip_countImpl(){
        return new[]{1,2,3,4}.Skip(1).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Skip_count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Skip>(nameof(Skip_count_enumerableImpl));
        var expected=SystemEnumerable.Skip(new[]{1,2,3,4},1).ToList();
        var actual=Skip_count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Skip_count_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Skip(1).ToList();
    }
}
