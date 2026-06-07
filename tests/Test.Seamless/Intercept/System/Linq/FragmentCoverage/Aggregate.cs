using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Aggregate {
    [Fact,Trait("Category","Unit")]
    public static void Aggregate_func(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_funcImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},static (a,v) => a+v);
        var actual=Aggregate_funcImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_funcImpl(){
        return new[]{1,2,3,4}.Aggregate(static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_seed_func(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_seed_funcImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},10,static (a,v) => a+v);
        var actual=Aggregate_seed_funcImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_seed_funcImpl(){
        return new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_seed_func_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_seed_func_resultSelectorImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},10,static (a,v) => a+v,static a => a*2);
        var actual=Aggregate_seed_func_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_seed_func_resultSelectorImpl(){
        return new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v,static a => a*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_func_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_func_enumerableImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},static (a,v) => a+v);
        var actual=Aggregate_func_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_func_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Aggregate(static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_seed_func_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_seed_func_enumerableImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},10,static (a,v) => a+v);
        var actual=Aggregate_seed_func_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_seed_func_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Aggregate(10,static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_seed_func_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_seed_func_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},10,static (a,v) => a+v,static a => a*2);
        var actual=Aggregate_seed_func_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_seed_func_resultSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Aggregate(10,static (a,v) => a+v,static a => a*2);
    }
}
