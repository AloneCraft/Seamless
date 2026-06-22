using System.Collections.Generic;
using System.Linq;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class ToHashSet {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.ToHashSet().OrderBy(static v => v).ToList();
        var actual=noArgImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static global::System.Collections.Generic.HashSet<int> noArgImpl(){
        return new[]{1,2,3,4}.ToHashSet();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(comparerImpl));
        var expected=new[]{1,2,3,4}.ToHashSet(EqualityComparer<int>.Default).OrderBy(static v => v).ToList();
        var actual=comparerImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static global::System.Collections.Generic.HashSet<int> comparerImpl(){
        return new[]{1,2,3,4}.ToHashSet(EqualityComparer<int>.Default);
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.ToHashSet().OrderBy(static v => v).ToList();
        var actual=noArg_enumerableImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static global::System.Collections.Generic.HashSet<int> noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToHashSet();
    }

    [Fact,Trait("Category","Unit")]
    public static void comparer_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ToHashSet>(nameof(comparer_enumerableImpl));
        var expected=new[]{1,2,3,4}.ToHashSet(EqualityComparer<int>.Default).OrderBy(static v => v).ToList();
        var actual=comparer_enumerableImpl().OrderBy(static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static global::System.Collections.Generic.HashSet<int> comparer_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ToHashSet(EqualityComparer<int>.Default);
    }
}
