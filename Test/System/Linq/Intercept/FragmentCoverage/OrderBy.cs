namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class OrderBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(keySelectorImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v).ToList();
        var actual=keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectorImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(comparerImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v,Comparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_keySelectorImpl));
        var expected=new[]{3,1,4,2}.OrderByDescending(static v => v).ToList();
        var actual=OrderByDescending_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_keySelectorImpl(){
        return new[]{3,1,4,2}.OrderByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_comparerImpl));
        var expected=new[]{3,1,4,2}.OrderByDescending(static v => v,Comparer<int>.Default).ToList();
        var actual=OrderByDescending_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_comparerImpl(){
        return new[]{3,1,4,2}.OrderByDescending(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderDescending_consume_preserves_equal_item_order(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderDescending_consume_preserves_equal_item_orderImpl));
        var a1=new string('a',1);
        var a2=new string('a',1);
        var b1=new string('b',1);
        var b2=new string('b',1);
        var actual=new Consumer<string>();
        OrderDescending_consume_preserves_equal_item_orderImpl([a1,a2,b1,b2],actual);
        Assert.Collection(actual.Items,
            item=>Assert.Same(b1,item),
            item=>Assert.Same(b2,item),
            item=>Assert.Same(a1,item),
            item=>Assert.Same(a2,item));
    }
    private static void OrderDescending_consume_preserves_equal_item_orderImpl(string[]source,Consumer<string>consumer){
        source.OrderDescending().Consume(consumer);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(keySelector_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v).ToList();
        var actual=keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(comparer_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v,Comparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_keySelector_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderByDescending(static v => v).ToList();
        var actual=OrderByDescending_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_keySelector_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<OrderBy>(nameof(OrderByDescending_comparer_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderByDescending(static v => v,Comparer<int>.Default).ToList();
        var actual=OrderByDescending_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OrderByDescending_comparer_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderByDescending(static v => v,Comparer<int>.Default).ToList();
    }
}
