namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class ThenBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        var generated=GeneratedInterceptorAssert.SourceFor<ThenBy>(nameof(keySelectorImpl));
        Assert.Contains("global::System.Buffers.ArrayPool<int>.Shared.Rent",generated);
        Assert.Contains("global::System.Array.Sort(__orderIndexesLocal",generated);
        Assert.Contains("__orderIndexComparer",generated);
        Assert.DoesNotContain("global::System.Collections.Generic.Comparer<int>.Create",generated);
        Assert.DoesNotContain("global::System.Collections.Generic.List<SortItem>",generated);
        Assert.DoesNotContain("items.Sort",generated);
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v).ToList();
        var actual=keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectorImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(comparerImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v,Comparer<int>.Default).ToList();
        var actual=comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenByDescending_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenByDescending_keySelectorImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
        var actual=ThenByDescending_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenByDescending_keySelectorImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenByDescending_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenByDescending_comparerImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v,Comparer<int>.Default).ToList();
        var actual=ThenByDescending_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenByDescending_comparerImpl(){
        return new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(keySelector_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v).ToList();
        var actual=keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderBy(static v => v%2).ThenBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(comparer_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenBy(static v => v,Comparer<int>.Default).ToList();
        var actual=comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderBy(static v => v%2).ThenBy(static v => v,Comparer<int>.Default).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenByDescending_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenByDescending_keySelector_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
        var actual=ThenByDescending_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenByDescending_keySelector_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ThenByDescending_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ThenBy>(nameof(ThenByDescending_comparer_enumerableImpl));
        var expected=new[]{3,1,4,2}.OrderBy(static v => v%2).ThenByDescending(static v => v,Comparer<int>.Default).ToList();
        var actual=ThenByDescending_comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ThenByDescending_comparer_enumerableImpl(){
        IEnumerable<int>src=[3,1,4,2];
        return src.OrderBy(static v => v%2).ThenByDescending(static v => v,Comparer<int>.Default).ToList();
    }
}
