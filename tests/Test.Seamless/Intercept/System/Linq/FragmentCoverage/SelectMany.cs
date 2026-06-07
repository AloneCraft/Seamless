using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class SelectMany {
    [Fact,Trait("Category","Unit")]
    public static void SelectMany_selector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_selectorImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static v => new[]{v,v+10}).ToList();
        var actual=SelectMany_selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_selectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_indexSelector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_indexSelectorImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static (v,i) => new[]{v,i}).ToList();
        var actual=SelectMany_indexSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_indexSelectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_selector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_selector_resultSelectorImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
        var actual=SelectMany_selector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_selector_resultSelectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_indexSelector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_indexSelector_resultSelectorImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
        var actual=SelectMany_indexSelector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_indexSelector_resultSelectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_selector_enumerableImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static v => new[]{v,v+10}).ToList();
        var actual=SelectMany_selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_selector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SelectMany(static v => new[]{v,v+10}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_indexSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_indexSelector_enumerableImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static (v,i) => new[]{v,i}).ToList();
        var actual=SelectMany_indexSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_indexSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SelectMany(static (v,i) => new[]{v,i}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_selector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_selector_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
        var actual=SelectMany_selector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_selector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SelectMany(static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectMany_indexSelector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(SelectMany_indexSelector_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.SelectMany(new[]{1,2,3,4},static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
        var actual=SelectMany_indexSelector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SelectMany_indexSelector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SelectMany(static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
    }
}
