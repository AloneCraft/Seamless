using System.Collections.Generic;
using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class SelectMany {
    [Fact,Trait("Category","Unit")]
    public static void selector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(selectorImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10}).ToList();
        var actual=selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> selectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void indexSelector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(indexSelectorImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i}).ToList();
        var actual=indexSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexSelectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(selector_resultSelectorImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
        var actual=selector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> selector_resultSelectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void indexSelector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(indexSelector_resultSelectorImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
        var actual=indexSelector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexSelector_resultSelectorImpl(){
        return new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(selector_enumerableImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10}).ToList();
        var actual=selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> selector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.SelectMany(static v => new[]{v,v+10}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void indexSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(indexSelector_enumerableImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i}).ToList();
        var actual=indexSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexSelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.SelectMany(static (v,i) => new[]{v,i}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(selector_resultSelector_enumerableImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
        var actual=selector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> selector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.SelectMany(static v => new[]{v,v+10},static (v,sub) => sub+v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void indexSelector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SelectMany>(nameof(indexSelector_resultSelector_enumerableImpl));
        var expected=new[]{1,2,3,4}.SelectMany(static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
        var actual=indexSelector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexSelector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.SelectMany(static (v,i) => new[]{v,i},static (v,sub) => sub+v).ToList();
    }
}
