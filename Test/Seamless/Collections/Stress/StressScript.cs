namespace Test.Seamless.Collections.Stress;

/// <summary>
/// 豎ｺ螳夊ｫ也噪 Stress 繧ｹ繧ｯ繝ｪ繝励ヨ逕滓・蝎ｨ縲・
/// 蜷御ｸ繧ｷ繝ｼ繝峨〒蠢・★蜷後§謫堺ｽ懷・繝ｻ蜷後§繧ｭ繝ｼ蛻励ｒ蜃ｺ縺吶・
/// </summary>
internal static class StressScript {
    /// <summary>
    /// [0, range) 縺九ｉ縺ｮ髱樣㍾隍・く繝ｼ驟榊・繧定ｿ斐☆縲・isher-Yates 繧ｷ繝｣繝・ヵ繝ｫ縺ｧ豎ｺ螳夊ｫ也噪縲・
    /// <paramref name="count"/> 縺ｯ <paramref name="range"/> 莉･荳九・
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

    /// <summary>Remove 蟇ｾ雎｡縺ｮ繝槭せ繧ｯ・・ength 蛟九》rue 邇・removeRate・峨・/summary>
    public static bool[] RemoveMask(int seed,int length,double removeRate) {
        var rng = new Random(seed);
        var mask = new bool[length];
        for(var i = 0; i<length; i++)
            mask[i]=rng.NextDouble()<removeRate;
        return mask;
    }
}
