using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;
using Test.Seamless.Intercept.System.Linq.Chain1;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

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

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_enumerableImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v);
        var actual=ToLookup_keySelector_enumerableImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.ToLookup(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_elementSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_elementSelector_enumerableImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v,static v => v*2);
        var actual=ToLookup_keySelector_elementSelector_enumerableImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_elementSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.ToLookup(static v => v,static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_comparer_enumerableImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v,EqualityComparer<int>.Default);
        var actual=ToLookup_keySelector_comparer_enumerableImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.ToLookup(static v => v,EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void ToLookup_keySelector_elementSelector_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToLookup>(nameof(ToLookup_keySelector_elementSelector_comparer_enumerableImpl));
        var expected=SystemEnumerable.ToLookup(new[]{1,1,2,2},static v => v,static v => v*2,EqualityComparer<int>.Default);
        var actual=ToLookup_keySelector_elementSelector_comparer_enumerableImpl();
        LinqChain1TestBase.AssertLookupEqual(expected,actual);
    }
    private static ILookup<int,int> ToLookup_keySelector_elementSelector_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.ToLookup(static v => v,static v => v*2,EqualityComparer<int>.Default);
    }
}
