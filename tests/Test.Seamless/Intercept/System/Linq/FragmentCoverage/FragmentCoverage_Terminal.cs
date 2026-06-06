using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;
using Test.Seamless.Intercept.System.Linq.Chain1;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class ToList {
    [Fact,Trait("Category","Unit")]
    public static void ToList_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToList>(nameof(ToList_noArgImpl));
        var expected=SystemEnumerable.ToList(new[]{1,2,3,4});
        var actual=ToList_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ToList_noArgImpl(){
        return new[]{1,2,3,4}.ToList();
    }
}

public sealed class ToArray {
    [Fact,Trait("Category","Unit")]
    public static void ToArray_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToArray>(nameof(ToArray_noArgImpl));
        var expected=SystemEnumerable.ToArray(new[]{1,2,3,4});
        var actual=ToArray_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int[] ToArray_noArgImpl(){
        return new[]{1,2,3,4}.ToArray();
    }
}

public sealed class ToHashSet {
    [Fact,Trait("Category","Unit")]
    public static void ToHashSet_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(ToHashSet_noArgImpl));
        var expected=SystemEnumerable.ToHashSet(new[]{1,2,3,4}).OrderBy(static v => v).ToList();
        var actual=ToHashSet_noArgImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static HashSet<int> ToHashSet_noArgImpl(){
        return new[]{1,2,3,4}.ToHashSet();
    }

    [Fact,Trait("Category","Unit")]
    public static void ToHashSet_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(ToHashSet_comparerImpl));
        var expected=SystemEnumerable.ToHashSet(new[]{1,2,3,4},EqualityComparer<int>.Default).OrderBy(static v => v).ToList();
        var actual=ToHashSet_comparerImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static HashSet<int> ToHashSet_comparerImpl(){
        return new[]{1,2,3,4}.ToHashSet(EqualityComparer<int>.Default);
    }
}

public sealed class ToDictionary {
    [Fact,Trait("Category","Unit")]
    public static void ToDictionary_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(ToDictionary_keySelectorImpl));
        var expected=SystemEnumerable.ToDictionary(new[]{1,2,3,4},static v => v);
        var actual=ToDictionary_keySelectorImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> ToDictionary_keySelectorImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToDictionary_keySelector_elementSelector(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(ToDictionary_keySelector_elementSelectorImpl));
        var expected=SystemEnumerable.ToDictionary(new[]{1,2,3,4},static v => v,static v => v*2);
        var actual=ToDictionary_keySelector_elementSelectorImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> ToDictionary_keySelector_elementSelectorImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToDictionary_keySelector_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(ToDictionary_keySelector_comparerImpl));
        var expected=SystemEnumerable.ToDictionary(new[]{1,2,3,4},static v => v,EqualityComparer<int>.Default);
        var actual=ToDictionary_keySelector_comparerImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> ToDictionary_keySelector_comparerImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v,EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToDictionary_keySelector_elementSelector_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(ToDictionary_keySelector_elementSelector_comparerImpl));
        var expected=SystemEnumerable.ToDictionary(new[]{1,2,3,4},static v => v,static v => v*2,EqualityComparer<int>.Default);
        var actual=ToDictionary_keySelector_elementSelector_comparerImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> ToDictionary_keySelector_elementSelector_comparerImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2,EqualityComparer<int>.Default);
    }
}

public sealed class ToLookup {
    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelectorImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v);
        var actual=ToLookup_keySelectorImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelectorImpl(){
        return new[]{1,1,2,2}.ToLookup(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_elementSelector(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_elementSelectorImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v,static v => v*2);
        var actual=ToLookup_keySelector_elementSelectorImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_elementSelectorImpl(){
        return new[]{1,1,2,2}.ToLookup(static v => v,static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_comparerImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v,EqualityComparer<int>.Default);
        var actual=ToLookup_keySelector_comparerImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_comparerImpl(){
        return new[]{1,1,2,2}.ToLookup(static v => v,EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_elementSelector_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_elementSelector_comparerImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v,static v => v*2,EqualityComparer<int>.Default);
        var actual=ToLookup_keySelector_elementSelector_comparerImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_elementSelector_comparerImpl(){
        return new[]{1,1,2,2}.ToLookup(static v => v,static v => v*2,EqualityComparer<int>.Default);
    }
}

public sealed class CountBy {
    [Fact,Trait("Category","Unit")]
    public static void CountBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<CountBy>(nameof(CountBy_keySelectorImpl));
        var expected=SystemEnumerable.CountBy(new[]{1,1,2,2},static v => v).OrderBy(static kv => kv.Key).ToList();
        var actual=CountBy_keySelectorImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> CountBy_keySelectorImpl(){
        return new[]{1,1,2,2}.CountBy(static v => v).ToList();
    }
}
