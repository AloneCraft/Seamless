using System.Collections.Generic;
using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Concat {
    [Fact,Trait("Category","Unit")]
    public static void second(){
        GeneratedInterceptorAssert.ContainsMethod<Concat>(nameof(secondImpl));
        var expected=new[]{1,2,3}.Concat([4,5,6]).ToList();
        var actual=secondImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> secondImpl(){
        return new[]{1,2,3}.Concat([4,5,6]).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void second_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Concat>(nameof(second_enumerableImpl));
        var expected=new[]{1,2,3}.Concat([4,5,6]).ToList();
        var actual=second_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> second_enumerableImpl(){
        IEnumerable<int>first=[1,2,3];
        IEnumerable<int>second=[4,5,6];
        return first.Concat(second).ToList();
    }
}
