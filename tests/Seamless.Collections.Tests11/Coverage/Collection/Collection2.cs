using Common;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// src/Seamless.Collections11/Collection/Collection`2.cs の全分岐をカバーする。
/// Collections11版独自のRemove()オーバーライド(DeleteSessionFunctionsのRMW経由)の分岐。
/// </summary>
[Collection("Sequential")]
public class Collection2 {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    //Collection`2.Remove(TKey): RMWベース削除の IsPending/非Pending / Found/NotFound の4分岐
    [Fact, Trait("Category","Coverage")]
    public void Remove_Mutable() {
        //ミュータブル領域のキーを削除 (IsPending=false経路)
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        //存在するキーを削除 → true (Found)
        Assert.True(dict.Remove(1));
        Assert.False(dict.ContainsKey(1));
        //存在しないキーを削除 → false (NotFound)
        Assert.False(dict.Remove(9999));
    }
    //Collection`2.Remove(TKey): IsPending経路 (ReadOnly領域のキーを削除)
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Remove_IsPending() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        //データを追加してReadOnly領域に押し込む
        for(var a = 0; a<500; a++) {
            dict.Add(a,$"v{a}");
        }
        //ReadOnly領域のキーを削除 → IsPending発生後にFound=true
        Assert.True(dict.Remove(0));
        Assert.False(dict.ContainsKey(0));
        //存在しないキーを削除 → false
        Assert.False(dict.Remove(9999));
    }
    //Collection`2.Dispose(bool): deleteSession の二重Disposeガード
    [Fact, Trait("Category","Coverage")]
    public void Dispose_AlreadyDisposed() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        dict.Add(1,"a");
        dict.Dispose();
        //二重Dispose: deleteSessionDisposed=true で if(!disposed) がfalseになる
        dict.Dispose();
        store.Dispose();
    }
    //Collection`2.Dispose(bool): finalizer経路 (disposing=false)
    [Fact, Trait("Category","Coverage")]
    public void Dispose_Finalizer() {
        {
            var store = new DictionaryStore<int,string>();
            var dict = store.Create();
            dict.Add(1,"a");
            dict=null!;
            store=null!;
        }
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
    }
}
