using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class OrderBy {
    [Fact,Trait("Category","Unit")]
    public static void OrderBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderBy_keySelectorImpl));
        var expected=SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v).ToList();
        var actual=OrderBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderBy_keySelectorImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderBy_comparerImpl));
        var expected=SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v,Comparer<int>.Default).ToList();
        var actual=OrderBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderBy_comparerImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_keySelectorImpl));
        var expected=SystemEnumerable.OrderByDescending(new[]{3,1,4,2},static v => v).ToList();
        var actual=OrderByDescending_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_keySelectorImpl(){
        return new[]{3,1,4,2}.OrderByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_comparerImpl));
        var expected=SystemEnumerable.OrderByDescending(new[]{3,1,4,2},static v => v,Comparer<int>.Default).ToList();
        var actual=OrderByDescending_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_comparerImpl(){
        return new[]{3,1,4,2}.OrderByDescending(static v => v,Comparer<int>.Default).ToList();
    }
}

public sealed class ThenBy {
    [Fact,Trait("Category","Unit")]
    public static void ThenBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenBy_keySelectorImpl));
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v%2),static v => v).ToList();
        var actual=ThenBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenBy_keySelectorImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenBy_comparerImpl));
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v%2),static v => v,Comparer<int>.Default).ToList();
        var actual=ThenBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenBy_comparerImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenByDescending_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenByDescending_keySelectorImpl));
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v%2),static v => v).ToList();
        var actual=ThenByDescending_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenByDescending_keySelectorImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenByDescending_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenByDescending_comparerImpl));
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v%2),static v => v,Comparer<int>.Default).ToList();
        var actual=ThenByDescending_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenByDescending_comparerImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v,Comparer<int>.Default).ToList();
    }
}
