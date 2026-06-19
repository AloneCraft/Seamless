using System.Collections.Generic;
using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Distinct {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(noArgImpl));
        var expected=new[]{1,2,2,3}.Distinct().ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,2,3}.Distinct().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(comparerImpl));
        var expected=new[]{1,2,2,3}.Distinct(EqualityComparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{1,2,2,3}.Distinct(EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,2,3}.Distinct().ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,2,3];
        return src.Distinct().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(comparer_enumerableImpl));
        var expected=new[]{1,2,2,3}.Distinct(EqualityComparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>src=[1,2,2,3];
        return src.Distinct(EqualityComparer<int>.Default).ToList();
    }
}
