namespace Test.System.Linq.Intercept;

// 2026-09-30 の差分ファズで確認して直した 2 つの不具合。
//  - 連鎖(Select など)の末尾の ToDictionary が、インデクサ代入で重複キーを黙って上書きしていた
//    (System.Linq は ArgumentException。件数が減り、要素が消えるのに例外にならない)
//  - Take(Range) が、範囲が件数を超えると ArgumentOutOfRangeException になっていた
//    (System.Linq は [0, 件数] へ切り詰める。Take(1..4) を 2 要素に、Take(^3..) を 1 要素に付けると例外だった)
public sealed class ToDictionaryAndTakeRange {
    private static List<int> WithDuplicates() => [9,-2,4,4,0,7,-2,12,3];
    private static List<int> Unique() => [9,-2,4,0,7,12,3];

    [Fact, Trait("Category","Unit")]
    public void ToDictionary_AfterSelect_ThrowsOnDuplicateKeyLikeSystemLinq() {
        Assert.Throws<ArgumentException>(() => SelectToDictionary_NoOptimized(WithDuplicates()));
        Assert.Throws<ArgumentException>(() => SelectToDictionary_Optimized(WithDuplicates()));
        Assert.Throws<ArgumentException>(() => WhereToDictionaryKeyOnly_Optimized(WithDuplicates()));
        Assert.Equal(SelectToDictionary_NoOptimized(Unique()),SelectToDictionary_Optimized(Unique()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SelectToDictionary_Optimized(List<int> source) => source.Select(x => x*3).ToDictionary(x => x,x => x*2).Count;

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int SelectToDictionary_NoOptimized(List<int> source) => source.Select(x => x*3).ToDictionary(x => x,x => x*2).Count;

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WhereToDictionaryKeyOnly_Optimized(List<int> source) => source.Where(x => x>-5).ToDictionary(x => x).Count;

    [Fact, Trait("Category","Unit")]
    public void TakeRange_BeyondTheSequence_IsClampedLikeSystemLinq() {
        Assert.Equal(EndBeyond_NoOptimized([1,2]),EndBeyond_Optimized([1,2]));
        Assert.Equal(FromEndBeyond_NoOptimized([3]),FromEndBeyond_Optimized([3]));
        Assert.Equal(FromEndBeyond_NoOptimized([]),FromEndBeyond_Optimized([]));
        Assert.Equal(FromEndThenSkip_NoOptimized([1,2]),FromEndThenSkip_Optimized([1,2]));
        Assert.Equal(BothFromEndBeyond_NoOptimized([1,2]),BothFromEndBeyond_Optimized([1,2]));
        Assert.Equal(Middle_NoOptimized(WithDuplicates()),Middle_Optimized(WithDuplicates()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> EndBeyond_Optimized(List<int> source) => source.Select(x => x).Take(1..4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> EndBeyond_NoOptimized(List<int> source) => source.Select(x => x).Take(1..4).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> FromEndBeyond_Optimized(List<int> source) => source.Select(x => x).Take(^3..).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> FromEndBeyond_NoOptimized(List<int> source) => source.Select(x => x).Take(^3..).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> FromEndThenSkip_Optimized(List<int> source) => source.Select(x => x).Take(^3..).Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> FromEndThenSkip_NoOptimized(List<int> source) => source.Select(x => x).Take(^3..).Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> BothFromEndBeyond_Optimized(List<int> source) => source.Select(x => x).Take(^9..^5).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> BothFromEndBeyond_NoOptimized(List<int> source) => source.Select(x => x).Take(^9..^5).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> Middle_Optimized(List<int> source) => source.Select(x => x).Take(2..^1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> Middle_NoOptimized(List<int> source) => source.Select(x => x).Take(2..^1).ToList();
}
