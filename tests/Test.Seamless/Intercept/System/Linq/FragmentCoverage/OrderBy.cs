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

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v).ToList();
        var actual=OrderBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderBy_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{3,1,4,2};
        return src.OrderBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderBy_comparer_enumerableImpl));
        var expected=SystemEnumerable.OrderBy(new[]{3,1,4,2},static v => v,Comparer<int>.Default).ToList();
        var actual=OrderBy_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderBy_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{3,1,4,2};
        return src.OrderBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_keySelector_enumerableImpl));
        var expected=SystemEnumerable.OrderByDescending(new[]{3,1,4,2},static v => v).ToList();
        var actual=OrderByDescending_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{3,1,4,2};
        return src.OrderByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_comparer_enumerableImpl));
        var expected=SystemEnumerable.OrderByDescending(new[]{3,1,4,2},static v => v,Comparer<int>.Default).ToList();
        var actual=OrderByDescending_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{3,1,4,2};
        return src.OrderByDescending(static v => v,Comparer<int>.Default).ToList();
    }
}
