using Seamless.Stores;
namespace Test.Seamless.Unit.Stores;

/// <summary>
/// 4種類のDictionaryがログサイズに差が出ることを確認するテスト。
/// int キー + int 値 で N 件挿入後の TailAddress を比較する。
/// </summary>
public partial class KindDictionarySize {
    public partial void UnmanagedUnmanaged_vs_ManagedManaged_SizeIsSmaller() {
        using var uuStore = Factory.Create(MemorySize: MemorySize).Create<int,int>();
        using var uuDict = uuStore.Create();
        var uuBytes = MeasureLogBytes(() => uuStore.Store.Log.TailAddress,uuDict);

        using var mmStore = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),new ManagedTag());
        using var mmDict = mmStore.Create();
        var mmBytes = MeasureLogBytes(() => mmStore.Store.Log.TailAddress,mmDict);

        // UnmanagedUnmanaged は生バイト格納 → ManagedManaged 以下 (2.0beta3ではアラインメントにより等しい場合あり)
        Assert.True(uuBytes==mmBytes,
            $"UnmanagedUnmanaged({uuBytes}) > ManagedManaged({mmBytes})");
    }
    public partial void ManagedUnmanaged_vs_ManagedManaged_ValuePartDiffers() {
        using var muStore = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),default);
        using var muDict = muStore.Create();
        var muBytes = MeasureLogBytes(() => muStore.Store.Log.TailAddress,muDict);

        using var mmStore = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),new ManagedTag());
        using var mmDict = mmStore.Create();
        var mmBytes = MeasureLogBytes(() => mmStore.Store.Log.TailAddress,mmDict);

        // ManagedUnmanaged は値を生バイトで格納するため ManagedManaged 以下
        Assert.True(muBytes<=mmBytes,
            $"ManagedUnmanaged({muBytes}) > ManagedManaged({mmBytes})");
    }
    public partial void UnmanagedManaged_vs_ManagedManaged_ValuePartDiffers() {
        using var muStore = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),new ManagedTag());
        using var muDict = muStore.Create();
        var muBytes = MeasureLogBytes(() => muStore.Store.Log.TailAddress,muDict);

        using var mmStore = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),default);
        using var mmDict = mmStore.Create();
        var mmBytes = MeasureLogBytes(() => mmStore.Store.Log.TailAddress,mmDict);

        // TSAVORITE1_1_1では SpanByte キーはSpanByte値としか組み合わせられないため、
        // ManagedUnmanagedは実質的にManagedManagedと同じサイズになります。
        // つまり、Tsavoriteの型安全性により両者は必ず等しいサイズになります。
        Assert.True(muBytes==mmBytes,
            $"ManagedUnmanaged({muBytes}) > ManagedManaged({mmBytes})");
    }
    public partial void OutputsLogSizesForAllFourKinds() {
        var sizes = new System.Collections.Generic.Dictionary<string,long>();

        {
            using var store = Factory.Create(MemorySize: MemorySize).Create<int,int>();
            using var dict = store.Create();
            sizes["UnmanagedUnmanaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }
        {
            using var store = Factory.Create(MemorySize: MemorySize).Create<int,int>(default,new ManagedTag());
            using var dict = store.Create();
            sizes["UnmanagedManaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }
        {
            using var store = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),default);
            using var dict = store.Create();
            sizes["ManagedUnmanaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }
        {
            using var store = Factory.Create(MemorySize: MemorySize).Create<int,int>(new ManagedTag(),new ManagedTag());
            using var dict = store.Create();
            sizes["ManagedManaged"]=MeasureLogBytes(() => store.Store.Log.TailAddress,dict);
        }

        var expected = GetExpectedSizes();

        // 4種類それぞれの計測結果を固定順で出力
        foreach(var kind in KindOrder)
            output.WriteLine($"{kind}: {sizes[kind]} bytes ({sizes[kind]/N} bytes/entry)");

        foreach(var kind in KindOrder)
            Assert.Equal(expected[kind],sizes[kind]);
    }

    private static partial System.Collections.Generic.Dictionary<string,long> GetExpectedSizes() {
        return new System.Collections.Generic.Dictionary<string,long> {
            ["UnmanagedUnmanaged"]=24320,
            ["UnmanagedManaged"]=24320,
            ["ManagedUnmanaged"]=24320,
            ["ManagedManaged"]=24320,
        };
        throw new InvalidOperationException("Expected constants for TSAVORITE version are not defined.");
    }
}
