using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

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

    [Fact,Trait("Category","Unit")]
    public static void Chunk_size_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Chunk>(nameof(Chunk_size_enumerableImpl));
        var expected=SystemEnumerable.Chunk(new[]{1,2,3,4},2).SelectMany(static a => a).ToList();
        var actual=Chunk_size_enumerableImpl().SelectMany(static a => a).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<int[]> Chunk_size_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Chunk(2).ToList();
    }
}
