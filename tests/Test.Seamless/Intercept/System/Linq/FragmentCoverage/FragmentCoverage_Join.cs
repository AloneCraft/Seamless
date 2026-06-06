using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;
using Test.Seamless.Intercept.System.Linq.Chain1;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Join {
    [Fact,Trait("Category","Unit")]
    public static void Join_keySelectors_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Join>(nameof(Join_keySelectors_resultSelectorImpl));
        var expected=SystemEnumerable.Join(new[]{1,2,3},new[]{2,3,4},static o => o,static i => i,static (o,i) => o+i).ToList();
        var actual=Join_keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Join_keySelectors_resultSelectorImpl(){
        return new[]{1,2,3}.Join(new[]{2,3,4},static o => o,static i => i,static (o,i) => o+i).ToList();
    }
}

public sealed class GroupJoin {
    [Fact,Trait("Category","Unit")]
    public static void GroupJoin_keySelectors_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<GroupJoin>(nameof(GroupJoin_keySelectors_resultSelectorImpl));
        var expected=SystemEnumerable.GroupJoin(new[]{1,2,3},new[]{2,3,4},static o => o,static i => i,static (o,ig) => o).ToList();
        var actual=GroupJoin_keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> GroupJoin_keySelectors_resultSelectorImpl(){
        return new[]{1,2,3}.GroupJoin(new[]{2,3,4},static o => o,static i => i,static (o,ig) => o).ToList();
    }
}

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
}
