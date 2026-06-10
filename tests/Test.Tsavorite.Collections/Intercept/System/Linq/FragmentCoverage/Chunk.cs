using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class Chunk {
    [Fact,Trait("Category","Unit")]
    public static void size(){
        GeneratedInterceptorAssert.ContainsMethod<Chunk>(nameof(sizeImpl));
        var expected=SystemEnumerable.Chunk([1,2,3,4],2).SelectMany(static a => a).ToList();
        var actual=sizeImpl().SelectMany(static a => a).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<int[]> sizeImpl(){
        return new[]{1,2,3,4}.Chunk(2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void size_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Chunk>(nameof(size_enumerableImpl));
        var expected=SystemEnumerable.Chunk([1,2,3,4],2).SelectMany(static a => a).ToList();
        var actual=size_enumerableImpl().SelectMany(static a => a).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<int[]> size_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Chunk(2).ToList();
    }
}
