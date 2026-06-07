using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

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

    [Fact,Trait("Category","Unit")]
    public static void ToHashSet_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(ToHashSet_noArg_enumerableImpl));
        var expected=SystemEnumerable.ToHashSet(new[]{1,2,3,4}).OrderBy(static v => v).ToList();
        var actual=ToHashSet_noArg_enumerableImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static HashSet<int> ToHashSet_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.ToHashSet();
    }

    [Fact,Trait("Category","Unit")]
    public static void ToHashSet_comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(ToHashSet_comparer_enumerableImpl));
        var expected=SystemEnumerable.ToHashSet(new[]{1,2,3,4},EqualityComparer<int>.Default).OrderBy(static v => v).ToList();
        var actual=ToHashSet_comparer_enumerableImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static HashSet<int> ToHashSet_comparer_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.ToHashSet(EqualityComparer<int>.Default);
    }
}
