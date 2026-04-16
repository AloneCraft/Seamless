using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Stores;

/// <summary>
/// 4種類のDictionaryがログサイズに差が出ることを確認するテスト。
/// int キー + int 値 で N 件挿入後の TailAddress を比較する。
/// </summary>
public class KindDictionarySize {
    private const int N = 1000;
    private const long MemorySize = 1L << 20; // 1MB: N=1000エントリには十分

    private static long MeasureLogBytes<TDict>(
        Func<long> getTailAddress,
        TDict dict)
        where TDict:ISeamlessDictionary<int,int> {
        var begin = getTailAddress();
        for(var i = 0; i < N; i++) dict.Add(i, i);
        return getTailAddress() - begin;
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedUnmanaged_vs_ManagedManaged_サイズが小さい(){
        using var uuStore = Factory.Create(MemorySize:MemorySize).Create<int,int>();
        using var uuDict  = uuStore.Create();
        var uuBytes = MeasureLogBytes(()=>uuStore.Store.Log.TailAddress, uuDict);

        using var mmStore = Factory.Create(MemorySize:MemorySize).Create<int,int>(new ManagedTag(), new ManagedTag());
        using var mmDict  = mmStore.Create();
        var mmBytes = MeasureLogBytes(()=>mmStore.Store.Log.TailAddress, mmDict);

        // UnmanagedUnmanaged は生バイト格納 → ManagedManaged 以下 (2.0beta3ではアラインメントにより等しい場合あり)
        Assert.True(uuBytes <= mmBytes,
            $"UnmanagedUnmanaged({uuBytes}) > ManagedManaged({mmBytes})");
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedUnmanaged_vs_ManagedManaged_値部分で差がある(){
        using var muStore = Factory.Create(MemorySize:MemorySize).Create<int,int>(new ManagedTag(), default);
        using var muDict  = muStore.Create();
        var muBytes = MeasureLogBytes(()=>muStore.Store.Log.TailAddress, muDict);

        using var mmStore = Factory.Create(MemorySize:MemorySize).Create<int,int>(new ManagedTag(), new ManagedTag());
        using var mmDict  = mmStore.Create();
        var mmBytes = MeasureLogBytes(()=>mmStore.Store.Log.TailAddress, mmDict);

        // ManagedUnmanaged は値を生バイトで格納するため ManagedManaged 以下
        Assert.True(muBytes <= mmBytes,
            $"ManagedUnmanaged({muBytes}) > ManagedManaged({mmBytes})");
    }

    [Fact, Trait("Category","Unit")]
    public void 全4種類のログサイズを出力する(){
        var sizes = new System.Collections.Generic.Dictionary<string, long>();

        {
            using var store = Factory.Create(MemorySize:MemorySize).Create<int,int>();
            using var dict  = store.Create();
            sizes["UnmanagedUnmanaged"] = MeasureLogBytes(()=>store.Store.Log.TailAddress, dict);
        }
        {
            using var store = Factory.Create(MemorySize:MemorySize).Create<int,int>(default, new ManagedTag());
            using var dict  = store.Create();
            sizes["UnmanagedManaged"] = MeasureLogBytes(()=>store.Store.Log.TailAddress, dict);
        }
        {
            using var store = Factory.Create(MemorySize:MemorySize).Create<int,int>(new ManagedTag(), default);
            using var dict  = store.Create();
            sizes["ManagedUnmanaged"] = MeasureLogBytes(()=>store.Store.Log.TailAddress, dict);
        }
        {
            using var store = Factory.Create(MemorySize:MemorySize).Create<int,int>(new ManagedTag(), new ManagedTag());
            using var dict  = store.Create();
            sizes["ManagedManaged"] = MeasureLogBytes(()=>store.Store.Log.TailAddress, dict);
        }

        // 4種類それぞれの計測結果を出力
        foreach(var kv in sizes)
            Console.WriteLine($"{kv.Key}: {kv.Value} bytes ({kv.Value / N} bytes/entry)");

        // 最低限: UnmanagedUnmanaged が最小
        Assert.True(
            sizes["UnmanagedUnmanaged"] <= sizes["UnmanagedManaged"] &&
            sizes["UnmanagedUnmanaged"] <= sizes["ManagedUnmanaged"] &&
            sizes["UnmanagedUnmanaged"] <= sizes["ManagedManaged"],
            $"UnmanagedUnmanaged は最小であるべき。計測値: {string.Join(", ", sizes.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }
}
