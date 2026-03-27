using Common;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// src/Seamless.Collections11/Store/CollectionStore`2.cs の全分岐をカバーする。
/// Collections11版はSpanByteAllocatorベースだが外部APIは同一。
/// </summary>
[Collection("Sequential")]
public class CollectionStore2 {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    //CollectionStore コンストラクタ: ObjectLogDevice.Capacity<0 の分岐
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Constructor_ObjectLogCapacity_Negative() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        //Capacityを指定しないとデフォルト=-1になる(Capacity<0分岐を通す)
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: TestMemorySize);
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Equal("a",dict[1]);
    }
    //CollectionStore.CompactWithLookup: CompactUntil<=BeginAddress の早期return分岐
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void CompactWithLookup_EarlyReturn() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: TestMemorySize,MutableFraction: 0.1);
        using var dict = store.Create();
        for(var a = 0; a<1000; a++) {
            dict.Add(a,$"v{a}");
        }
        {
            var before = store.Address;
            store.CompactWithLookup();
            var after = store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        {
            var beforeSecond = store.Address;
            store.CompactWithLookup();
            var afterSecond = store.Address;
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
        //連続呼び出し: 早期return経路をカバー
        store.CompactWithLookup();
        store.CompactWithLookup();
    }
    //CollectionStore.CompactWithLookup: CompactUntil>SafeReadOnlyAddress でクリップされる分岐
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void CompactWithLookup_ClipToSafeReadOnly() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var address = store.Address;
        store.CompactWithLookup();
        Assert.True(store.Address.BeginAddress>=address.BeginAddress);
    }
    //CollectionStore.Dispose: 二重Dispose
    [Fact, Trait("Category","Coverage")]
    public void Dispose_Twice() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        dict.Dispose();
        store.Dispose();
        //二重Dispose
        store.Dispose();
    }
    //CollectionStore.Address: 各アドレスプロパティを取得
    [Fact, Trait("Category","Coverage")]
    public void Address() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var (begin,head,readOnly,tail)=store.Address;
        Assert.True(tail>=begin);
        Assert.True(head>=begin);
        Assert.True(readOnly>=begin);
    }
}
