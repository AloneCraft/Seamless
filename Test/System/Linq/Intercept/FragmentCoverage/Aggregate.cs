namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Aggregate {
    [Fact,Trait("Category","Unit")]
    public static void func(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(funcImpl));
        var expected=new[]{1,2,3,4}.Aggregate(static (a,v) => a+v);
        var actual=funcImpl();
        Assert.Equal(expected,actual);
    }
    private static int funcImpl(){
        return new[]{1,2,3,4}.Aggregate(static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void seed_func(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(seed_funcImpl));
        var expected=new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v);
        var actual=seed_funcImpl();
        Assert.Equal(expected,actual);
    }
    private static int seed_funcImpl(){
        return new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void seed_func_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(seed_func_resultSelectorImpl));
        var expected=new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v,static a => a*2);
        var actual=seed_func_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int seed_func_resultSelectorImpl(){
        return new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v,static a => a*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void func_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(func_enumerableImpl));
        var expected=new[]{1,2,3,4}.Aggregate(static (a,v) => a+v);
        var actual=func_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int func_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Aggregate(static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void seed_func_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(seed_func_enumerableImpl));
        var expected=new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v);
        var actual=seed_func_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int seed_func_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Aggregate(10,static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void seed_func_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(seed_func_resultSelector_enumerableImpl));
        var expected=new[]{1,2,3,4}.Aggregate(10,static (a,v) => a+v,static a => a*2);
        var actual=seed_func_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int seed_func_resultSelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Aggregate(10,static (a,v) => a+v,static a => a*2);
    }
}
