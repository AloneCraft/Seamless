using System.Collections.Generic;
using System.Linq;


using Test.System.Linq.Intercept.Chain1;

namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class ToDictionary {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelectorImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v);
        var actual=keySelectorImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelectorImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_elementSelectorImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2);
        var actual=keySelector_elementSelectorImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_elementSelectorImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_comparerImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v,EqualityComparer<int>.Default);
        var actual=keySelector_comparerImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_comparerImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v,EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_elementSelector_comparerImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2,EqualityComparer<int>.Default);
        var actual=keySelector_elementSelector_comparerImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_elementSelector_comparerImpl(){
        return new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2,EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_enumerableImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v);
        var actual=keySelector_enumerableImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToDictionary(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_elementSelector_enumerableImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2);
        var actual=keySelector_elementSelector_enumerableImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_elementSelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToDictionary(static v => v,static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_comparer_enumerableImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v,EqualityComparer<int>.Default);
        var actual=keySelector_comparer_enumerableImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_comparer_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToDictionary(static v => v,EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToDictionary>(nameof(keySelector_elementSelector_comparer_enumerableImpl));
        var expected=new[]{1,2,3,4}.ToDictionary(static v => v,static v => v*2,EqualityComparer<int>.Default);
        var actual=keySelector_elementSelector_comparer_enumerableImpl();
        LinqChain1TestBase.AssertDictionaryEqual(expected,actual);
    }
    private static Dictionary<int,int> keySelector_elementSelector_comparer_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToDictionary(static v => v,static v => v*2,EqualityComparer<int>.Default);
    }
}
