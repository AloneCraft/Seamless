using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Stores;

/// <summary>
/// 4種類のDictionaryがログサイズに差が出ることを確認するテスト。
/// int キー + int 値 で N 件挿入後の TailAddress を比較する。
/// </summary>
public partial class KindDictionarySize(ITestOutputHelper output) {
    private const int N = 1000;
    private const long MemorySize = 1L<<20; // 1MB: N=1000エントリには十分
    private static readonly string[] KindOrder = [
        "UnmanagedUnmanaged",
        "UnmanagedManaged",
        "ManagedUnmanaged",
        "ManagedManaged"
    ];

    private static long MeasureLogBytes<TDict>(
        Func<long> getTailAddress,
        TDict dict)
        where TDict : ISeamlessDictionary<int,int> {
        var begin = getTailAddress();
        for(var i = 0; i<N; i++) dict.Add(i,i);
        return getTailAddress()-begin;
    }

    [Fact, Trait("Category","Unit")]
    public partial void UnmanagedUnmanaged_vs_ManagedManaged_サイズが小さい();
    [Fact, Trait("Category","Unit")]
    public partial void ManagedUnmanaged_vs_ManagedManaged_値部分で差がある();
    [Fact, Trait("Category","Unit")]
    public partial void UnmanagedManaged_vs_ManagedManaged_値部分で差がある();
    [Fact, Trait("Category","Unit")]
    public partial void 全4種類のログサイズを出力する();
    private static partial System.Collections.Generic.Dictionary<string,long> GetExpectedSizes();
}
