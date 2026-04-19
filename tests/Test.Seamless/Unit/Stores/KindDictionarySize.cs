using Seamless.Collections;
namespace Test.Seamless.Unit.Stores;

/// <summary>
/// 4��ނ�Dictionary�����O�T�C�Y�ɍ����o�邱�Ƃ�m�F����e�X�g�B
/// int �L�[ + int �l �� N ���}����� TailAddress ���r����B
/// </summary>
public partial class KindDictionarySize(ITestOutputHelper output) {
    private const int N = 1000;
    private const long MemorySize = 1L<<20; // 1MB: N=1000�G���g���ɂ͏\��
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
    public partial void UnmanagedUnmanaged_vs_ManagedManaged_SizeIsSmaller();
    [Fact, Trait("Category","Unit")]
    public partial void ManagedUnmanaged_vs_ManagedManaged_ValuePartDiffers();
    [Fact, Trait("Category","Unit")]
    public partial void UnmanagedManaged_vs_ManagedManaged_ValuePartDiffers();
    [Fact, Trait("Category","Unit")]
    public partial void OutputsLogSizesForAllFourKinds();
    private static partial System.Collections.Generic.Dictionary<string,long> GetExpectedSizes();
}
