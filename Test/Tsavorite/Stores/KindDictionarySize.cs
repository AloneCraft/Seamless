using Tsavorite.Stores;
namespace Test.Tsavorite.Stores;

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
        where TDict : global::Tsavorite.Collections.IDictionary<int,int> {
        var begin = getTailAddress();
        for(var i = 0; i<N; i++) dict.Add(i,i);
        return getTailAddress()-begin;
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedUnmanaged_vs_ManagedManaged_SizeIsSmaller() {
        using var uuStore = Store.Create(MemorySize: MemorySize).Create<int,int>();
        using var uuDict = uuStore.Create();
        var uuBytes = MeasureLogBytes(() => uuStore.Store.Log.TailAddress,uuDict);

        using var mmStore = Store.Create(MemorySize: MemorySize).Create<int,int>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        using var mmDict = mmStore.Create();
        var mmBytes = MeasureLogBytes(() => mmStore.Store.Log.TailAddress,mmDict);

        AssertUnmanagedUnmanagedVsManagedManaged(uuBytes,mmBytes);
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedUnmanaged_vs_ManagedManaged_ValuePartDiffers() {
        using var muStore = Store.Create(MemorySize: MemorySize).Create<int,int>(new MemoryPackSerializerStrategy(),new BlittableStrategy());
        using var muDict = muStore.Create();
        var muBytes = MeasureLogBytes(() => muStore.Store.Log.TailAddress,muDict);

        using var mmStore = Store.Create(MemorySize: MemorySize).Create<int,int>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        using var mmDict = mmStore.Create();
        var mmBytes = MeasureLogBytes(() => mmStore.Store.Log.TailAddress,mmDict);

        AssertManagedUnmanagedVsManagedManaged(muBytes,mmBytes);
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedManaged_vs_ManagedManaged_ValuePartDiffers() {
        using var umStore = Store.Create(MemorySize: MemorySize).Create<int,int>(new BlittableStrategy(),new MemoryPackSerializerStrategy());
        using var umDict = umStore.Create();
        var umBytes = MeasureLogBytes(() => umStore.Store.Log.TailAddress,umDict);

        using var mmStore = Store.Create(MemorySize: MemorySize).Create<int,int>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        using var mmDict = mmStore.Create();
        var mmBytes = MeasureLogBytes(() => mmStore.Store.Log.TailAddress,mmDict);

        AssertUnmanagedManagedVsManagedManaged(umBytes,mmBytes);
    }

    [Fact, Trait("Category","Unit")]
    public void OutputsLogSizesForAllFourKinds() {
        var sizes = new Dictionary<string,long>();

        {
            using var store = Store.Create(MemorySize: MemorySize).Create<int,int>();
            using var dict = store.Create();
            sizes["UnmanagedUnmanaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }
        {
            using var store = Store.Create(MemorySize: MemorySize).Create<int,int>(new BlittableStrategy(),new MemoryPackSerializerStrategy());
            using var dict = store.Create();
            sizes["UnmanagedManaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }
        {
            using var store = Store.Create(MemorySize: MemorySize).Create<int,int>(new MemoryPackSerializerStrategy(),new BlittableStrategy());
            using var dict = store.Create();
            sizes["ManagedUnmanaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }
        {
            using var store = Store.Create(MemorySize: MemorySize).Create<int,int>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
            using var dict = store.Create();
            sizes["ManagedManaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }

        var expected = GetExpectedSizes();

        foreach(var kind in KindOrder)
            output.WriteLine($"{kind}: {sizes[kind]} bytes ({sizes[kind]/N} bytes/entry)");

        foreach(var kind in KindOrder)
            Assert.Equal(expected[kind],sizes[kind]);
    }

    private static partial void AssertUnmanagedUnmanagedVsManagedManaged(long uuBytes,long mmBytes);
    private static partial void AssertManagedUnmanagedVsManagedManaged(long muBytes,long mmBytes);
    private static partial void AssertUnmanagedManagedVsManagedManaged(long umBytes,long mmBytes);
    private static partial Dictionary<string,long> GetExpectedSizes();
}
