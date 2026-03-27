using Common;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// src/Seamless.Collections11/Collection/HashSet.cs の全分岐をカバーする。
/// </summary>
[Collection("Sequential")]
public class HashSet_Coverage {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    //HashSet.Dispose(bool disposing): disposing=false (ファイナライザ経路) の分岐
    [Fact, Trait("Category","Coverage")]
    public void Dispose_Finalizer() {
        {
            var store = new HashSetStore<int>();
            var set = store.Create();
            set.Add(1);
            set=null!;
            store=null!;
        }
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
    }
    //HashSet.Dispose(bool): SetOperationSessionsDisposed=true の二重Dispose
    [Fact, Trait("Category","Coverage")]
    public void Dispose_AlreadyDisposed() {
        var store = new HashSetStore<int>();
        var set = store.Create();
        set.Add(1);
        set.Dispose();
        set.Dispose();
        store.Dispose();
    }
    //HashSet.IntersectWith: IsPending経路 - 読み取り専用領域のキーを削除するRMW
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void IntersectWith_IsPending() {
        var folder1 = CreateFolder();
        using var LogDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store1 = new HashSetStore<int>(LogDevice1,ObjDevice1,MemorySize: TestMemorySize,MutableFraction: 0);
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        for(var a = 0; a<500; a++) {
            set1.Add(a);
        }
        for(var a = 100; a<300; a++) {
            set2.Add(a);
        }
        //IntersectWith: 読み取り専用領域のキーをRMW削除 (IsPending経路)
        set1.IntersectWith(set2);
        Assert.DoesNotContain(0,set1);
        Assert.Contains(100,set1);
        Assert.Contains(299,set1);
        Assert.DoesNotContain(499,set1);
    }
    //HashSet.SymmetricExceptWith: IsPending経路
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void SymmetricExceptWith_IsPending() {
        var folder1 = CreateFolder();
        using var LogDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store1 = new HashSetStore<int>(LogDevice1,ObjDevice1,MemorySize: TestMemorySize,MutableFraction: 0);
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        for(var a = 0; a<500; a++) {
            set1.Add(a);
        }
        for(var a = 400; a<600; a++) {
            set2.Add(a);
        }
        //SymmetricExceptWith: 読み取り専用領域のキーでRMWが発生 (IsPending経路)
        set1.SymmetricExceptWith(set2);
        Assert.Contains(0,set1);
        Assert.Contains(399,set1);
        Assert.DoesNotContain(450,set1);
        Assert.Contains(550,set1);
    }
}
