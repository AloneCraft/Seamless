namespace Tsavorite.Collection.Stress;

/// <summary>
/// Stress 繝・せ繝育畑縺ｮ荵ｱ謨ｰ繝ｦ繝ｼ繝・ぅ繝ｪ繝・ぅ縲・/// 蜀咲樟蜿ｯ閭ｽ縺ｪ繧ｭ繝ｼ蛻励→蜑企勁繝槭せ繧ｯ繧堤函謌舌☆繧九・/// </summary>
internal static class StressScript {
    /// <summary>
    /// [0, range) 縺九ｉ驥崎､・↑縺励・繧ｭ繝ｼ繧・count 莉ｶ繧ｵ繝ｳ繝励Μ繝ｳ繧ｰ縺吶ｋ縲・    /// Fisher-Yates 繧ｷ繝｣繝・ヵ繝ｫ繧剃ｽｿ縺・◆繧√…ount 縺ｯ range 莉･荳九〒縺ゅｋ蠢・ｦ√′縺ゅｋ縲・    /// </summary>
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
    /// length 莉ｶ縺ｮ蜑企勁繝槭せ繧ｯ繧剃ｽ懈・縺吶ｋ縲ょ推隕∫ｴ縺・true 縺ｫ縺ｪ繧狗｢ｺ邇・・ removeRate縲・    /// </summary>
    public static bool[] RemoveMask(int seed,int length,double removeRate) {
        var rng = new Random(seed);
        var mask = new bool[length];
        for(var i = 0; i<length; i++)
            mask[i]=rng.NextDouble()<removeRate;
        return mask;
    }
}
