namespace Tsavorite.Collection.Shared.Stress;

/// <summary>
/// Stress テスト用の乱数ユーティリティ。
/// 再現可能なキー列と削除マスクを生成する。
/// </summary>
internal static class StressScript {
    /// <summary>
    /// [0, range) から重複なしのキーを count 件サンプリングする。
    /// Fisher-Yates シャッフルを使うため、count は range 以下である必要がある。
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

    /// <summary>
    /// length 件の削除マスクを作成する。各要素が true になる確率は removeRate。
    /// </summary>
    public static bool[] RemoveMask(int seed,int length,double removeRate) {
        var rng = new Random(seed);
        var mask = new bool[length];
        for(var i = 0; i<length; i++)
            mask[i]=rng.NextDouble()<removeRate;
        return mask;
    }
}
