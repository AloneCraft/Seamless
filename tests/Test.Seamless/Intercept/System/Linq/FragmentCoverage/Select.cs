using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Select {
    [Fact,Trait("Category","Unit")]
    public static void Select_selector(){
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(Select_selectorImpl));
        var expected=SystemEnumerable.Select(new[]{1,2,3,4},static v => v*2).ToList();
        var actual=Select_selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_selectorImpl(){
        return new[]{1,2,3,4}.Select(static v => v*2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Select_indexSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(Select_indexSelectorImpl));
        var expected=SystemEnumerable.Select(new[]{1,2,3,4},static (v,i) => v+i).ToList();
        var actual=Select_indexSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_indexSelectorImpl(){
        return new[]{1,2,3,4}.Select(static (v,i) => v+i).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Select_selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(Select_selector_enumerableImpl));
        var expected=SystemEnumerable.Select(new[]{1,2,3,4},static v => v*2).ToList();
        var actual=Select_selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_selector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Select(static v => v*2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Select_indexSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(Select_indexSelector_enumerableImpl));
        var expected=SystemEnumerable.Select(new[]{1,2,3,4},static (v,i) => v+i).ToList();
        var actual=Select_indexSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Select_indexSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Select(static (v,i) => v+i).ToList();
    }
}
