using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Except {
    [Fact,Trait("Category","Unit")]
    public static void Except_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(Except_noArgImpl));
        var expected=SystemEnumerable.Except(new[]{1,2,3,4},new[]{3,4,5}).ToList();
        var actual=Except_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_noArgImpl(){
        return new[]{1,2,3,4}.Except(new[]{3,4,5}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Except_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(Except_comparerImpl));
        var expected=SystemEnumerable.Except(new[]{1,2,3,4},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Except_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_comparerImpl(){
        return new[]{1,2,3,4}.Except(new[]{3,4,5},EqualityComparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Except_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(Except_noArg_enumerableImpl));
        var expected=SystemEnumerable.Except(new[]{1,2,3,4},new[]{3,4,5}).ToList();
        var actual=Except_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_noArg_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.Except(second).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Except_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(Except_comparer_enumerableImpl));
        var expected=SystemEnumerable.Except(new[]{1,2,3,4},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Except_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_comparer_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3,4};
        IEnumerable<int>second=new[]{3,4,5};
        return first.Except(second,EqualityComparer<int>.Default).ToList();
    }
}
