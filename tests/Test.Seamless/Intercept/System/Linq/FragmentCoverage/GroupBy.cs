using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;
using Test.Seamless.Intercept.System.Linq.Chain1;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class GroupBy {
    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelectorImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v).ToList();
        var actual=GroupBy_keySelectorImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> GroupBy_keySelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_resultSelectorImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v,static (k,g) => k).ToList();
        var actual=GroupBy_keySelector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_keySelector_resultSelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v,static (k,g) => k).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_elementSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_elementSelectorImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v,static v => v*2).ToList();
        var actual=GroupBy_keySelector_elementSelectorImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> GroupBy_keySelector_elementSelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v,static v => v*2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_elementSelector_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_elementSelector_resultSelectorImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v,static v => v*2,static (k,g) => k).ToList();
        var actual=GroupBy_keySelector_elementSelector_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_keySelector_elementSelector_resultSelectorImpl(){
        return new[]{1,1,2,2}.GroupBy(static v => v,static v => v*2,static (k,g) => k).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v).ToList();
        var actual=GroupBy_keySelector_enumerableImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> GroupBy_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.GroupBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v,static (k,g) => k).ToList();
        var actual=GroupBy_keySelector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_keySelector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.GroupBy(static v => v,static (k,g) => k).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_elementSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_elementSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v,static v => v*2).ToList();
        var actual=GroupBy_keySelector_elementSelector_enumerableImpl();
        LinqChain1TestBase.AssertGroupSequenceEqual(expected,actual);
    }
    private static List<IGrouping<int,int>> GroupBy_keySelector_elementSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.GroupBy(static v => v,static v => v*2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupBy_keySelector_elementSelector_resultSelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<GroupBy>(nameof(GroupBy_keySelector_elementSelector_resultSelector_enumerableImpl));
        var expected=SystemEnumerable.GroupBy(new[]{1,1,2,2},static v => v,static v => v*2,static (k,g) => k).ToList();
        var actual=GroupBy_keySelector_elementSelector_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupBy_keySelector_elementSelector_resultSelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,1,2,2};
        return src.GroupBy(static v => v,static v => v*2,static (k,g) => k).ToList();
    }
}
