using Common;
using Garnet.common;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tsavorite.core;
using Xunit;

namespace Seamless.Collections.Tests;

// src/Seamless.Collections/Collection/Collection`5.cs の主要分岐を Dictionary API 経由でカバーする。
[Collection("Sequential")]
public class Collection5 {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    [Fact, Trait("Category","Coverage")]
    public void ctor() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.NotNull(dict);
        Assert.Equal(0,dict.Count);
    }

    // Collection.ThrowLogFull / WouldOverflowMutable を Dictionary.Add 経由で確認
    [Fact, Trait("Category","Coverage")]
    public void ThrowLogFull_WouldOverflowMutable() {
        using var store = new DictionaryStore<int,int>();
        using var dict = store.Create();

        Assert.Throws<OutOfMemoryException>(() => {
            for(var a = 0; a<10000; a++) {
                dict.Add(a,a);
            }
        });
    }

    // Collection.Serialize / Deserialize は Add -> TryGetValue で往復検証
    [Fact, Trait("Category","Coverage")]
    public void Serialize_Deserialize() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"abc");
        Assert.True(dict.TryGetValue(1,out var value));
        Assert.Equal("abc",value);
    }

    // Collection.GetLongCount / Count / EntryCount
    [Fact, Trait("Category","Coverage")]
    public void GetLongCount_Count_EntryCount() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        dict.Add(3,"c");

        Assert.Equal(3,dict.Count);
        Assert.True(dict.EntryCount>=3);
    }

    // Collection.Read / Pending / TryGetValue / PrivateContainsKey の IsPending 分岐
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Read_Pending_TryGetValue_PrivateContainsKey() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();

        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }

        Assert.True(dict.ContainsKey(0));
        Assert.True(dict.TryGetValue(0,out var value));
        Assert.Equal("v0",value);
    }

    // Collection.PrivateTryAdd / Remove / Clear / Dispose
    [Fact, Trait("Category","Coverage")]
    public void PrivateTryAdd_Remove_Clear_Dispose() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();

        Assert.Equal(TryAddResult.Success,dict.TryAdd(1,"a"));
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(1,"b"));
        Assert.True(dict.Remove(1));
        Assert.False(dict.Remove(999));

        dict.Add(2,"x");
        dict.Add(3,"y");
        dict.Clear();
        Assert.Equal(0,dict.Count);

        dict.Dispose();
        dict.Dispose();
        store.Dispose();
        store.Dispose();
    }

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Remove_IsPending() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();

        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }

        Assert.True(dict.Remove(0));
        Assert.False(dict.ContainsKey(0));
        Assert.False(dict.Remove(99999));
    }

    [Fact, Trait("Category","Coverage")]
    public void Remove_KeyValuePair() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        //キーが存在しない → false
        {
            var r = ((ICollection<KeyValuePair<int,string>>)dict).Remove(new KeyValuePair<int,string>(99,"x"));
            Assert.False(r);
        }
        dict.Add(1,"a");
        //キーは存在するが値が不一致 → false (elseブロック通過)
        {
            var r = ((ICollection<KeyValuePair<int,string>>)dict).Remove(new KeyValuePair<int,string>(1,"different"));
            Assert.False(r);
            Assert.True(dict.ContainsKey(1));
        }
        //キーも値も一致 → true
        {
            var r = ((ICollection<KeyValuePair<int,string>>)dict).Remove(new KeyValuePair<int,string>(1,"a"));
            Assert.True(r);
            Assert.False(dict.ContainsKey(1));
        }
    }
    //WouldOverflowMutable: LogMemorySize<=0 → false の早期returnをカバー
    [Fact, Trait("Category","Coverage")]
    public void WouldOverflowMutable_LogMemorySize0() {
        using var store = new DictionaryStore<int,string>();
        //LogMemorySize=0 → WouldOverflowMutable は即 false を返す
        using var dict = new Seamless.Collections.Dictionary<int,string>(store.Store,0,1.0);
        dict.Add(1,"a");
        Assert.Equal(1,dict.Count);
    }
    //ThrowLogFull: indexer setter 経由で throw this.ThrowLogFull() をカバー
    [Fact, Trait("Category","Coverage")]
    public void ThrowLogFull_ViaIndexerSetter() {
        using var store = new DictionaryStore<int,int>();
        using var dict = store.Create();
        Assert.Throws<OutOfMemoryException>(() => {
            for(var a = 0; a<10000; a++) {
                dict[a]=a;
            }
        });
    }
    //TryGetValue: IsPending 分岐 + NotFound (存在しないキーの pending 読み取り) をカバー
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void TryGetValue_Pending_NotFound() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }
        //存在しないキー → IsPending 可能性あり + NotFound=true → false を返す
        Assert.False(dict.TryGetValue(99999,out _));
    }
    //GetLongCount: ディスクに退避されたデータがある状態でも正しい件数を返すことをカバー
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void GetLongCount_WithPendingData() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }
        Assert.Equal(500L,dict.GetLongCount());
    }
    //Pending IsPending=true / TryGetValue IsPending=true+Found+NotFound をカバー
    //LogMemorySize=0でWouldOverflowMutableを無効化し、Tsavoriteストアを8KBの小メモリで構成。
    //500件挿入でメモリが溢れ古いレコードがディスクへ退避→IsPending=true分岐を通過。
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Pending_And_TryGetValue_IsPendingTrue() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        //Tsavoriteストアは8KB(2ページ分)で作成し、ディスクへの退避を促す
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: Const.LogMemorySize);
        //LogMemorySize=0→WouldOverflowMutableが常にfalseになり500件の挿入が可能
        using var dict = new Seamless.Collections.Dictionary<int,string>(store.Store,0,1.0);
        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }
        //IsPending=true+Found→値が正しく取得できる(Pending IsPending=trueブランチ通過)
        Assert.True(dict.TryGetValue(0,out var v));
        Assert.Equal("v0",v);
        //IsPending=true+NotFound→falseを返す
        Assert.False(dict.TryGetValue(99999,out _));
        //PrivateContainsKey経由のPendingカバー
        Assert.True(dict.ContainsKey(0));
        Assert.False(dict.ContainsKey(99999));
    }
    [Fact, Trait("Category","Coverage")]
    public void IEnumerable_GetEnumerator() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");

        var count = 0;
        IEnumerable nonGeneric = dict;
        foreach(var item in nonGeneric) {
            Assert.IsType<KeyValuePair<int,string>>(item);
            count++;
        }
        Assert.Equal(2,count);
    }
}
