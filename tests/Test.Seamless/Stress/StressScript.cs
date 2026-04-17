namespace Test.Seamless.Stress;

/// <summary>
/// 決定論的 Stress スクリプト生成器。
/// 同一シードで必ず同じ操作列・同じキー列を出す。
/// </summary>
internal static class StressScript {
    /// <summary>
    /// [0, range) からの非重複キー配列を返す。Fisher-Yates シャッフルで決定論的。
    /// <paramref name="count"/> は <paramref name="range"/> 以下。
    /// </summary>
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

    /// <summary>Remove 対象のマスク（length 個、true 率 removeRate）。</summary>
    public static bool[] RemoveMask(int seed,int length,double removeRate) {
        var rng = new Random(seed);
        var mask = new bool[length];
        for(var i = 0; i<length; i++)
            mask[i]=rng.NextDouble()<removeRate;
        return mask;
    }
}
