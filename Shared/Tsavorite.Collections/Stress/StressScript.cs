#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections.Stress;
#else
namespace Tsavorite.Collections.Stress;
#endif

/// <summary>
/// Stress テストの動作を検証する。
internal static class StressScript {
    /// <summary>
    /// Stress テストの動作を検証する。
    public static int[] SampleKeys(int seed,int range,int count) {
        if(count>range)
            throw new ArgumentOutOfRangeException(nameof(count));
        var rng = new Random(seed);
        var pool = new int[range];
        for(var i = 0; i<range; i++) pool[i]=i;
        for(var i = 0; i<count; i++) {
            var j = rng.Next(i,range);
            (pool[i],pool[j])=(pool[j],pool[i]);
        }
        Array.Resize(ref pool,count);
        return pool;
    }

    /// <summary>
    /// Stress テストの動作を検証する。
    public static bool[] RemoveMask(int seed,int length,double removeRate) {
        var rng = new Random(seed);
        var mask = new bool[length];
        for(var i = 0; i<length; i++)
            mask[i]=rng.NextDouble()<removeRate;
        return mask;
    }
}
