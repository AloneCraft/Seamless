using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Zip {
    [Fact,Trait("Category","Unit")]
    public static void Zip_second(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip_secondImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6}).ToList();
        var actual=Zip_secondImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int First,int Second)> Zip_secondImpl(){
        return new[]{1,2,3}.Zip(new[]{4,5,6}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip3_third(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip3_thirdImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6},new[]{7,8,9}).ToList();
        var actual=Zip3_thirdImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int First,int Second,int Third)> Zip3_thirdImpl(){
        return new[]{1,2,3}.Zip(new[]{4,5,6},new[]{7,8,9}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip_resultSelectorImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6},static (a,b) => a+b).ToList();
        var actual=Zip_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Zip_resultSelectorImpl(){
        return new[]{1,2,3}.Zip(new[]{4,5,6},static (a,b) => a+b).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip_second_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip_second_enumerableImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6}).ToList();
        var actual=Zip_second_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int First,int Second)> Zip_second_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{4,5,6};
        return first.Zip(second).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip3_third_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip3_third_enumerableImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6},new[]{7,8,9}).ToList();
        var actual=Zip3_third_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int First,int Second,int Third)> Zip3_third_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{4,5,6};
        IEnumerable<int>third=new[]{7,8,9};
        return first.Zip(second,third).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6},static (a,b) => a+b).ToList();
        var actual=Zip_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Zip_resultSelector_enumerableImpl(){
        IEnumerable<int>first=new[]{1,2,3};
        IEnumerable<int>second=new[]{4,5,6};
        return first.Zip(second,static (a,b) => a+b).ToList();
    }
}
