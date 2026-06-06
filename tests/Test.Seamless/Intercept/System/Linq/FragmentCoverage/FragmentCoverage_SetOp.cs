using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Distinct {
    [Fact,Trait("Category","Unit")]
    public static void Distinct_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(Distinct_noArgImpl));
        var expected=SystemEnumerable.Distinct(new[]{1,2,2,3}).ToList();
        var actual=Distinct_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Distinct_noArgImpl(){
        return new[]{1,2,2,3}.Distinct().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Distinct_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Distinct>(nameof(Distinct_comparerImpl));
        var expected=SystemEnumerable.Distinct(new[]{1,2,2,3},EqualityComparer<int>.Default).ToList();
        var actual=Distinct_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Distinct_comparerImpl(){
        return new[]{1,2,2,3}.Distinct(EqualityComparer<int>.Default).ToList();
    }
}

public sealed class DistinctBy {
    [Fact,Trait("Category","Unit")]
    public static void DistinctBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(DistinctBy_keySelectorImpl));
        var expected=SystemEnumerable.DistinctBy(new[]{1,2,2,3},static v => v).ToList();
        var actual=DistinctBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DistinctBy_keySelectorImpl(){
        return new[]{1,2,2,3}.DistinctBy(static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void DistinctBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<DistinctBy>(nameof(DistinctBy_comparerImpl));
        var expected=SystemEnumerable.DistinctBy(new[]{1,2,2,3},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=DistinctBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DistinctBy_comparerImpl(){
        return new[]{1,2,2,3}.DistinctBy(static v => v,EqualityComparer<int>.Default).ToList();
    }
}

public sealed class Union {
    [Fact,Trait("Category","Unit")]
    public static void Union_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(Union_noArgImpl));
        var expected=SystemEnumerable.Union(new[]{1,2,3},new[]{3,4,5}).ToList();
        var actual=Union_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_noArgImpl(){
        return new[]{1,2,3}.Union(new[]{3,4,5}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Union_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Union>(nameof(Union_comparerImpl));
        var expected=SystemEnumerable.Union(new[]{1,2,3},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Union_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_comparerImpl(){
        return new[]{1,2,3}.Union(new[]{3,4,5},EqualityComparer<int>.Default).ToList();
    }
}

public sealed class UnionBy {
    [Fact,Trait("Category","Unit")]
    public static void UnionBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<UnionBy>(nameof(UnionBy_keySelectorImpl));
        var expected=SystemEnumerable.UnionBy(new[]{1,2,3},new[]{3,4,5},static v => v).ToList();
        var actual=UnionBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> UnionBy_keySelectorImpl(){
        return new[]{1,2,3}.UnionBy(new[]{3,4,5},static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void UnionBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<UnionBy>(nameof(UnionBy_comparerImpl));
        var expected=SystemEnumerable.UnionBy(new[]{1,2,3},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=UnionBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> UnionBy_comparerImpl(){
        return new[]{1,2,3}.UnionBy(new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
    }
}

public sealed class Except {
    [Fact,Trait("Category","Unit")]
    public static void Except_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(Except_noArgImpl));
        var expected=SystemEnumerable.Except(new[]{1,2,3,4},new[]{3,4,5}).ToList();
        var actual=Except_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_noArgImpl(){
        return new[]{1,2,3,4}.Except(new[]{3,4,5}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Except_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Except>(nameof(Except_comparerImpl));
        var expected=SystemEnumerable.Except(new[]{1,2,3,4},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Except_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_comparerImpl(){
        return new[]{1,2,3,4}.Except(new[]{3,4,5},EqualityComparer<int>.Default).ToList();
    }
}

public sealed class ExceptBy {
    [Fact,Trait("Category","Unit")]
    public static void ExceptBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(ExceptBy_keySelectorImpl));
        var expected=SystemEnumerable.ExceptBy(new[]{1,2,3,4},new[]{3,4,5},static v => v).ToList();
        var actual=ExceptBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ExceptBy_keySelectorImpl(){
        return new[]{1,2,3,4}.ExceptBy(new[]{3,4,5},static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void ExceptBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(ExceptBy_comparerImpl));
        var expected=SystemEnumerable.ExceptBy(new[]{1,2,3,4},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=ExceptBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> ExceptBy_comparerImpl(){
        return new[]{1,2,3,4}.ExceptBy(new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
    }
}

public sealed class Intersect {
    [Fact,Trait("Category","Unit")]
    public static void Intersect_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Intersect>(nameof(Intersect_noArgImpl));
        var expected=SystemEnumerable.Intersect(new[]{1,2,3,4},new[]{3,4,5}).ToList();
        var actual=Intersect_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Intersect_noArgImpl(){
        return new[]{1,2,3,4}.Intersect(new[]{3,4,5}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Intersect_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<Intersect>(nameof(Intersect_comparerImpl));
        var expected=SystemEnumerable.Intersect(new[]{1,2,3,4},new[]{3,4,5},EqualityComparer<int>.Default).ToList();
        var actual=Intersect_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Intersect_comparerImpl(){
        return new[]{1,2,3,4}.Intersect(new[]{3,4,5},EqualityComparer<int>.Default).ToList();
    }
}

public sealed class IntersectBy {
    [Fact,Trait("Category","Unit")]
    public static void IntersectBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(IntersectBy_keySelectorImpl));
        var expected=SystemEnumerable.IntersectBy(new[]{1,2,3,4},new[]{3,4,5},static v => v).ToList();
        var actual=IntersectBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_keySelectorImpl(){
        return new[]{1,2,3,4}.IntersectBy(new[]{3,4,5},static v => v).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void IntersectBy_comparer(){
        GeneratedInterceptorAssert.ContainsMethod<IntersectBy>(nameof(IntersectBy_comparerImpl));
        var expected=SystemEnumerable.IntersectBy(new[]{1,2,3,4},new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
        var actual=IntersectBy_comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_comparerImpl(){
        return new[]{1,2,3,4}.IntersectBy(new[]{3,4,5},static v => v,EqualityComparer<int>.Default).ToList();
    }
}
