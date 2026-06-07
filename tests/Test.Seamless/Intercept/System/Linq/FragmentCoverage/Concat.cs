using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Concat {
    [Fact,Trait("Category","Unit")]
    public static void Concat_second(){
        GeneratedInterceptorAssert.ContainsMethod<Concat>(nameof(Concat_secondImpl));
        var expected=SystemEnumerable.Concat(new[]{1,2,3},new[]{4,5,6}).ToList();
        var actual=Concat_secondImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Concat_secondImpl(){
        return new[]{1,2,3}.Concat(new[]{4,5,6}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Concat_second_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Concat>(nameof(Concat_second_enumerableImpl));
        var expected=SystemEnumerable.Concat(new[]{1,2,3},new[]{4,5,6}).ToList();
        var actual=Concat_second_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Concat_second_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{4,5,6};
        return first.Concat(second).ToList();
    }
}
