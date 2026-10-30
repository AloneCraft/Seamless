namespace Benchmark.Dynamic.Core;

/// <summary>
/// int 要素の入力データ。Worker が作る入力と、UI が再現テストを書くときの入力を同じ規則にするため Core に置く。
/// 入力は連続する整数で、開始値だけをシードから決める。
/// </summary>
public static class FixtureValues{
    /// <param name="fixtureSeed">クエリごとの入力用シード(<c>SeedDerivation.Derive(基準シード,"fixture",QueryIndex)</c>)。</param>
    /// <param name="purpose">"primary" または "secondary"。</param>
    public static int StartValue(Seed256 fixtureSeed,string purpose,int elementCount){
        var derived=SeedDerivation.Derive(fixtureSeed,"fixture."+purpose);
        var random=new DeterministicPrng(derived);
        var centered=-(elementCount/2);
        var smallShift=random.NextInt32(-8,9);
        return checked(centered+smallShift);
    }

    public static int[] Values(Seed256 fixtureSeed,string purpose,int elementCount){
        return Enumerable.Range(StartValue(fixtureSeed,purpose,elementCount),elementCount).ToArray();
    }
}
