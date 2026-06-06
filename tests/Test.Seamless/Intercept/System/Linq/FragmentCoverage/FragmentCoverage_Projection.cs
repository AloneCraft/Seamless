using System.Collections;
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
}

public sealed class Where {
    [Fact,Trait("Category","Unit")]
    public static void Where_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(Where_predicateImpl));
        var expected=SystemEnumerable.Where(new[]{1,2,3,4},static v => v>2).ToList();
        var actual=Where_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_predicateImpl(){
        return new[]{1,2,3,4}.Where(static v => v>2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Where_indexPredicate(){
        GeneratedInterceptorAssert.ContainsMethod<Where>(nameof(Where_indexPredicateImpl));
        var expected=SystemEnumerable.Where(new[]{1,2,3,4},static (v,i) => i>1).ToList();
        var actual=Where_indexPredicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Where_indexPredicateImpl(){
        return new[]{1,2,3,4}.Where(static (v,i) => i>1).ToList();
    }
}

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
}

public sealed class Cast {
    [Fact,Trait("Category","Unit")]
    public static void Cast_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Cast>(nameof(Cast_noArgImpl));
        IEnumerable src=new object[]{1,2,3};
        var expected=SystemEnumerable.Cast<int>(src).ToList();
        var actual=Cast_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Cast_noArgImpl(){
        IEnumerable src=new object[]{1,2,3};
        return src.Cast<int>().ToList();
    }
}

public sealed class OfType {
    [Fact,Trait("Category","Unit")]
    public static void OfType_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<OfType>(nameof(OfType_noArgImpl));
        IEnumerable src=new object?[]{1,"x",2,null};
        var expected=SystemEnumerable.OfType<int>(src).ToList();
        var actual=OfType_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> OfType_noArgImpl(){
        IEnumerable src=new object?[]{1,"x",2,null};
        return src.OfType<int>().ToList();
    }
}
