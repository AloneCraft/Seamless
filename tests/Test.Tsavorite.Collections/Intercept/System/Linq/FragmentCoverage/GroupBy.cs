using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;
using Test.Tsavorite.Intercept.System.Linq.Chain1;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class GroupBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelectorImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v).ToList();
        var actual=keySelectorImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> keySelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_resultSelectorImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v,static (k,g) => k).ToList();
        var actual=keySelector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_resultSelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v,static (k,g) => k).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_elementSelectorImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v,static v => v*2).ToList();
        var actual=keySelector_elementSelectorImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> keySelector_elementSelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v,static v => v*2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_elementSelector_resultSelectorImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v,static v => v*2,static (k,g) => k).ToList();
        var actual=keySelector_elementSelector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_elementSelector_resultSelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v,static v => v*2,static (k,g) => k).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v).ToList();
        var actual=keySelector_enumerableImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> keySelector_enumerableImpl(){
        IEnumerable<int>src=[1,1,2,2];
        return src.GroupBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v,static (k,g) => k).ToList();
        var actual=keySelector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=[1,1,2,2];
        return src.GroupBy(static v => v,static (k,g) => k).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_elementSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v,static v => v*2).ToList();
        var actual=keySelector_elementSelector_enumerableImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> keySelector_elementSelector_enumerableImpl(){
        IEnumerable<int>src=[1,1,2,2];
        return src.GroupBy(static v => v,static v => v*2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_elementSelector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(keySelector_elementSelector_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy([1,1,2,2],static v => v,static v => v*2,static (k,g) => k).ToList();
        var actual=keySelector_elementSelector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_elementSelector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=[1,1,2,2];
        return src.GroupBy(static v => v,static v => v*2,static (k,g) => k).ToList();
    }
}
