using Common;
using System.Collections;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// src/Seamless.Collections/Collection/HashSet.cs の全分岐をカバーする。
/// Dictionary カバレッジと同じく API 単位で整理する。
/// </summary>
[Collection("Sequential")]
public class HashSet {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    [Fact, Trait("Category","Coverage")]
    public void IsReadOnly() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        Assert.False(set.IsReadOnly);
    }

    //HashSet.Add(T): Success / KeyAlreadyExists / LogFull の3分岐
    [Fact, Trait("Category","Coverage")]
    public void Add_item() {
        {
            using var store = new HashSetStore<int>();
            using var set = store.Create();

            Assert.True(set.Add(1));
            Assert.False(set.Add(1));
            Assert.Contains(1,set);
        }

        {
            var folder = CreateFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var store = new HashSetStore<string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0.01);
            using var set = store.Create();

            Assert.Throws<OutOfMemoryException>(() => {
                for(var a = 0; a<10000; a++) {
                    set.Add(new string((char)('a'+a%26),1024)+a);
                }
            });
        }
    }

    //ICollection<T>.Add(T)
    [Fact, Trait("Category","Coverage")]
    public void Add_ICollection() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();

        ((ICollection<int>)set).Add(10);

        Assert.Contains(10,set);
    }

    //HashSet.Contains(T)
    [Fact, Trait("Category","Coverage")]
    public void Contains() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);

        Assert.Contains(1,set);
        Assert.DoesNotContain(99,set);
    }

    //HashSet.CopyTo(T[],int): null / 負数 / 配列小 / 正常 の4分岐
    [Fact, Trait("Category","Coverage")]
    public void CopyTo() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!,0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[2],-1));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[1],0));

        {
            var array = new int[3];
            set.CopyTo(array,1);
            Assert.Equal(0,array[0]);
            Assert.Contains(1,array);
            Assert.Contains(2,array);
        }
    }

    //HashSet.ExceptWith(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void ExceptWith() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set1.Add(3);
        set2.Add(2);
        set2.Add(4);

        set1.ExceptWith(set2);

        Assert.Contains(1,set1);
        Assert.DoesNotContain(2,set1);
        Assert.Contains(3,set1);
        Assert.Throws<ArgumentNullException>(() => set1.ExceptWith(null!));
    }

    //HashSet.GetEnumerator(), IEnumerable.GetEnumerator()
    [Fact, Trait("Category","Coverage")]
    public void IEnumerable_GetEnumerator() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);

        var genericCount = 0;
        foreach(var item in set) {
            Assert.True(item==1||item==2);
            genericCount++;
        }
        Assert.Equal(2,genericCount);

        var nonGenericCount = 0;
        IEnumerable nonGeneric = set;
        foreach(var item in nonGeneric) {
            Assert.IsType<int>(item);
            nonGenericCount++;
        }
        Assert.Equal(2,nonGenericCount);
    }

    //HashSet.IntersectWith(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void IntersectWith() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set1.Add(3);
        set2.Add(2);
        set2.Add(3);
        set2.Add(4);

        set1.IntersectWith(set2);

        Assert.DoesNotContain(1,set1);
        Assert.Contains(2,set1);
        Assert.Contains(3,set1);
        Assert.Throws<ArgumentNullException>(() => set1.IntersectWith(null!));
    }

    //HashSet.IsProperSubsetOf(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void IsProperSubsetOf() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(1);
        set2.Add(2);
        set2.Add(3);

        Assert.True(set1.IsProperSubsetOf(set2));
        Assert.False(set2.IsProperSubsetOf(set1));

        using var store3 = new HashSetStore<int>();
        using var store4 = new HashSetStore<int>();
        using var set3 = store3.Create();
        using var set4 = store4.Create();
        set3.Add(1);
        set3.Add(9);
        set4.Add(1);
        set4.Add(2);
        set4.Add(3);

        Assert.False(set3.IsProperSubsetOf(set4));
        Assert.Throws<ArgumentNullException>(() => set1.IsProperSubsetOf(null!));
    }

    //HashSet.IsProperSupersetOf(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void IsProperSupersetOf() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set1.Add(3);
        set2.Add(1);
        set2.Add(2);

        Assert.True(set1.IsProperSupersetOf(set2));
        Assert.False(set2.IsProperSupersetOf(set1));

        //件数条件は満たすが、要素不足でループ内falseを返す分岐
        using var store3 = new HashSetStore<int>();
        using var store4 = new HashSetStore<int>();
        using var set3 = store3.Create();
        using var set4 = store4.Create();
        set3.Add(1);
        set3.Add(2);
        set3.Add(3);
        set4.Add(1);
        set4.Add(9);

        Assert.False(set3.IsProperSupersetOf(set4));
        Assert.Throws<ArgumentNullException>(() => set1.IsProperSupersetOf(null!));
    }

    //HashSet.IsSubsetOf(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void IsSubsetOf() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(1);
        set2.Add(2);
        set2.Add(3);

        Assert.True(set1.IsSubsetOf(set2));

        set1.Add(99);
        Assert.False(set1.IsSubsetOf(set2));
        Assert.Throws<ArgumentNullException>(() => set1.IsSubsetOf(null!));
    }

    //HashSet.IsSupersetOf(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void IsSupersetOf() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set1.Add(3);
        set2.Add(1);
        set2.Add(2);

        Assert.True(set1.IsSupersetOf(set2));

        set2.Add(9);
        Assert.False(set1.IsSupersetOf(set2));
        Assert.Throws<ArgumentNullException>(() => set1.IsSupersetOf(null!));
    }

    //HashSet.Overlaps(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void Overlaps() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(2);
        set2.Add(3);

        Assert.True(set1.Overlaps(set2));

        using var store3 = new HashSetStore<int>();
        using var store4 = new HashSetStore<int>();
        using var set3 = store3.Create();
        using var set4 = store4.Create();
        set3.Add(10);
        set4.Add(20);

        Assert.False(set3.Overlaps(set4));
        Assert.Throws<ArgumentNullException>(() => set1.Overlaps(null!));
    }

    //HashSet.SetEquals(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void SetEquals() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(1);
        set2.Add(2);

        Assert.True(set1.SetEquals(set2));

        set2.Add(3);
        Assert.False(set1.SetEquals(set2));

        using var store3 = new HashSetStore<int>();
        using var store4 = new HashSetStore<int>();
        using var set3 = store3.Create();
        using var set4 = store4.Create();
        set3.Add(1);
        set3.Add(9);
        set4.Add(1);
        set4.Add(2);

        Assert.False(set3.SetEquals(set4));
        Assert.Throws<ArgumentNullException>(() => set1.SetEquals(null!));
    }

    //HashSet.SymmetricExceptWith(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void SymmetricExceptWith() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(2);
        set2.Add(3);

        set1.SymmetricExceptWith(set2);

        Assert.Contains(1,set1);
        Assert.DoesNotContain(2,set1);
        Assert.Contains(3,set1);
        Assert.Throws<ArgumentNullException>(() => set1.SymmetricExceptWith(null!));
    }

    //HashSet.UnionWith(HashSet<T>)
    [Fact, Trait("Category","Coverage")]
    public void UnionWith() {
        using var store1 = new HashSetStore<int>();
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        set2.Add(1);

        set1.UnionWith(set2);

        Assert.Contains(1,set1);
        Assert.Contains(2,set1);
        Assert.Throws<ArgumentNullException>(() => set1.UnionWith(null!));
    }

    //HashSet.Dispose(bool): SetOperationSessionsDisposed=true のelse経路(二重Dispose)
    [Fact, Trait("Category","Coverage")]
    public void Dispose_AlreadyDisposed() {
        var store = new HashSetStore<int>();
        var set = store.Create();
        set.Add(1);

        set.Dispose();
        set.Dispose();
        store.Dispose();
    }

    //HashSet.Dispose(bool): disposing=false のファイナライザ経路
    [Fact, Trait("Category","Coverage")]
    public void Dispose_Finalizer() {
        WeakReference weakReference;
        {
            var store = new HashSetStore<int>();
            var set = store.Create();
            set.Add(1);
            weakReference=new WeakReference(set);
        }
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        Assert.NotNull(weakReference);
    }

    //HashSet.IntersectWith(HashSet<T>): IsPending経路
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void IntersectWith_IsPending() {
        var folder1 = CreateFolder();
        using var logDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store1 = new HashSetStore<int>(logDevice1,objDevice1,MemorySize: TestMemorySize,MutableFraction: 0);
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();

        for(var a = 0; a<500; a++) {
            set1.Add(a);
        }
        for(var a = 100; a<300; a++) {
            set2.Add(a);
        }

        set1.IntersectWith(set2);

        Assert.DoesNotContain(0,set1);
        Assert.Contains(100,set1);
        Assert.Contains(299,set1);
        Assert.DoesNotContain(499,set1);
    }

    //HashSet.SymmetricExceptWith(HashSet<T>): IsPending経路
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void SymmetricExceptWith_IsPending() {
        var folder1 = CreateFolder();
        using var logDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store1 = new HashSetStore<int>(logDevice1,objDevice1,MemorySize: TestMemorySize,MutableFraction: 0);
        using var store2 = new HashSetStore<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();

        for(var a = 0; a<500; a++) {
            set1.Add(a);
        }
        for(var a = 400; a<600; a++) {
            set2.Add(a);
        }

        set1.SymmetricExceptWith(set2);

        Assert.Contains(0,set1);
        Assert.Contains(399,set1);
        Assert.DoesNotContain(450,set1);
        Assert.Contains(550,set1);
    }
}

//using Common;
//using Tsavorite.core;
//using Xunit;
//namespace Seamless.Collections.Tests;
///// <summary>
///// src/Seamless.Collections11/Collection/HashSet.cs の全分岐をカバーする。
///// </summary>
//public class HashSet{
//    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
//    private const long TestMemorySize = Const.MinObjectLogCapacity;
//    //HashSet.Dispose(bool disposing): disposing=false (ファイナライザ経路) の分岐
//    [Fact, Trait("Category","Coverage")]
//    public void Dispose_Finalizer() {
//        {
//            var store = new HashSetStore<int>();
//            var set = store.Create();
//            set.Add(1);
//            set=null!;
//            store=null!;
//        }
//        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
//        GC.WaitForPendingFinalizers();
//        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
//        GC.WaitForPendingFinalizers();
//    }
//    //HashSet.Dispose(bool): SetOperationSessionsDisposed=true の二重Dispose
//    [Fact, Trait("Category","Coverage")]
//    public void Dispose_AlreadyDisposed() {
//        var store = new HashSetStore<int>();
//        var set = store.Create();
//        set.Add(1);
//        set.Dispose();
//        set.Dispose();
//        store.Dispose();
//    }
//    //HashSet.IntersectWith: IsPending経路 - 読み取り専用領域のキーを削除するRMW
//    [Fact, Trait("Category","Coverage")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
//    public void IntersectWith_IsPending() {
//        var folder1 = CreateFolder();
//        using var LogDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var ObjDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
//        using var store1 = new HashSetStore<int>(LogDevice1,ObjDevice1,MemorySize: TestMemorySize,MutableFraction: 0);
//        using var store2 = new HashSetStore<int>();
//        using var set1 = store1.Create();
//        using var set2 = store2.Create();
//        for(var a = 0; a<500; a++) {
//            set1.Add(a);
//        }
//        for(var a = 100; a<300; a++) {
//            set2.Add(a);
//        }
//        //IntersectWith: 読み取り専用領域のキーをRMW削除 (IsPending経路)
//        set1.IntersectWith(set2);
//        Assert.DoesNotContain(0,set1);
//        Assert.Contains(100,set1);
//        Assert.Contains(299,set1);
//        Assert.DoesNotContain(499,set1);
//    }
//    //HashSet.SymmetricExceptWith: IsPending経路
//    [Fact, Trait("Category","Coverage")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
//    public void SymmetricExceptWith_IsPending() {
//        var folder1 = CreateFolder();
//        using var LogDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var ObjDevice1 = new ManagedLocalStorageDevice(Path.Combine(folder1+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
//        using var store1 = new HashSetStore<int>(LogDevice1,ObjDevice1,MemorySize: TestMemorySize,MutableFraction: 0);
//        using var store2 = new HashSetStore<int>();
//        using var set1 = store1.Create();
//        using var set2 = store2.Create();
//        for(var a = 0; a<500; a++) {
//            set1.Add(a);
//        }
//        for(var a = 400; a<600; a++) {
//            set2.Add(a);
//        }
//        //SymmetricExceptWith: 読み取り専用領域のキーでRMWが発生 (IsPending経路)
//        set1.SymmetricExceptWith(set2);
//        Assert.Contains(0,set1);
//        Assert.Contains(399,set1);
//        Assert.DoesNotContain(450,set1);
//        Assert.Contains(550,set1);
//    }
//}
