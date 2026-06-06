using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Skip {
    [Fact,Trait("Category","Unit")]
    public static void Skip_count(){
        GeneratedInterceptorAssert.ContainsMethod<Skip>(nameof(Skip_countImpl));
        var expected=SystemEnumerable.Skip(new[]{1,2,3,4},1).ToList();
        var actual=Skip_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Skip_countImpl(){
        return new[]{1,2,3,4}.Skip(1).ToList();
    }
}

public sealed class SkipLast {
    [Fact,Trait("Category","Unit")]
    public static void SkipLast_count(){
        GeneratedInterceptorAssert.ContainsMethod<SkipLast>(nameof(SkipLast_countImpl));
        var expected=SystemEnumerable.SkipLast(new[]{1,2,3,4},1).ToList();
        var actual=SkipLast_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SkipLast_countImpl(){
        return new[]{1,2,3,4}.SkipLast(1).ToList();
    }
}

public sealed class SkipWhile {
    [Fact,Trait("Category","Unit")]
    public static void SkipWhile_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<SkipWhile>(nameof(SkipWhile_predicateImpl));
        var expected=SystemEnumerable.SkipWhile(new[]{1,2,3,4},static v => v<3).ToList();
        var actual=SkipWhile_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SkipWhile_predicateImpl(){
        return new[]{1,2,3,4}.SkipWhile(static v => v<3).ToList();
    }
}

public sealed class Take {
    [Fact,Trait("Category","Unit")]
    public static void Take_count(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(Take_countImpl));
        var expected=SystemEnumerable.Take(new[]{1,2,3,4},2).ToList();
        var actual=Take_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Take_countImpl(){
        return new[]{1,2,3,4}.Take(2).ToList();
    }
}

public sealed class TakeLast {
    [Fact,Trait("Category","Unit")]
    public static void TakeLast_count(){
        GeneratedInterceptorAssert.ContainsMethod<TakeLast>(nameof(TakeLast_countImpl));
        var expected=SystemEnumerable.TakeLast(new[]{1,2,3,4},2).ToList();
        var actual=TakeLast_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> TakeLast_countImpl(){
        return new[]{1,2,3,4}.TakeLast(2).ToList();
    }
}

public sealed class TakeWhile {
    [Fact,Trait("Category","Unit")]
    public static void TakeWhile_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<TakeWhile>(nameof(TakeWhile_predicateImpl));
        var expected=SystemEnumerable.TakeWhile(new[]{1,2,3,4},static v => v<3).ToList();
        var actual=TakeWhile_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> TakeWhile_predicateImpl(){
        return new[]{1,2,3,4}.TakeWhile(static v => v<3).ToList();
    }
}

public sealed class Chunk {
    [Fact,Trait("Category","Unit")]
    public static void Chunk_size(){
        GeneratedInterceptorAssert.ContainsMethod<Chunk>(nameof(Chunk_sizeImpl));
        var expected=SystemEnumerable.Chunk(new[]{1,2,3,4},2).SelectMany(static a => a).ToList();
        var actual=Chunk_sizeImpl().SelectMany(static a => a).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<int[]> Chunk_sizeImpl(){
        return new[]{1,2,3,4}.Chunk(2).ToList();
    }
}
