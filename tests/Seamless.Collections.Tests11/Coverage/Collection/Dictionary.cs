using Common;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// src/Seamless.Collections11/Collection/Dictionary.cs の全分岐をカバーする。
/// Collections版とは異なるSpanByteベース実装の分岐に注力する。
/// </summary>
[Collection("Sequential")]
public class Dictionary {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    //Dictionary.Add(TKey,TValue): switch TryAddResult.LogFull/KeyAlreadyExists/Success の3分岐
    [Fact, Trait("Category","Coverage")]
    public void Add() {
        //LogFull → OutOfMemoryException
        {
            using var store = new DictionaryStore<int,int>();
            using var dict = store.Create();
            Assert.Throws<OutOfMemoryException>(() => {
                for(var a = 0; a<10000; a++) {
                    dict.Add(a,a);
                }
            });
        }
        //KeyAlreadyExists → ArgumentException
        {
            using var store = new DictionaryStore<int,string>();
            using var dict = store.Create();
            dict.Add(1,"a");
            var ex = Assert.Throws<ArgumentException>(() => dict.Add(1,"b"));
            Assert.Contains("1",ex.Message);
        }
        //Success
        {
            using var store = new DictionaryStore<int,string>();
            using var dict = store.Create();
            dict.Add(99,"v99");
            Assert.Equal("v99",dict[99]);
        }
    }
    //Dictionary.Remove(KeyValuePair): 複合条件分岐
    //Collections11版は!TryGetValue||!Equals → false / 両方true → Remove の2分岐
    [Fact, Trait("Category","Coverage")]
    public void Remove_KeyValuePair() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        //キーが存在しない → false (TryGetValue=false で短絡評価)
        {
            var r = ((ICollection<KeyValuePair<int,string>>)dict).Remove(new KeyValuePair<int,string>(99,"x"));
            Assert.False(r);
        }
        dict.Add(1,"a");
        //キーは存在するが値が不一致 → false (TryGetValue=true, Equals=false)
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
    //Dictionary.CopyTo: null/負数/配列小/正常 の4分岐
    [Fact, Trait("Category","Coverage")]
    public void CopyTo() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Throws<ArgumentNullException>(() => ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(null!,0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(new KeyValuePair<int,string>[2],-1));
        Assert.Throws<ArgumentException>(() => ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(new KeyValuePair<int,string>[1],0));
        {
            var array = new KeyValuePair<int,string>[3];
            ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(array,1);
            Assert.Equal(2,array.Count(x => x.Value!=null));
        }
    }
    //Dictionary.this[TKey] getter: TryGetValue→true / Status.NotFound→throw の2分岐
    //Collections11版はNotFoundの判定がInlineで行われる
    [Fact, Trait("Category","Coverage")]
    public void Indexer_get() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        //存在しない → KeyNotFoundException
        Assert.Throws<KeyNotFoundException>(() => { var _ = dict[99]; });
        //存在する → 値を返す
        dict.Add(1,"hello");
        Assert.Equal("hello",dict[1]);
    }
    //Dictionary.this[TKey] setter: WouldOverflowMutable=true / 正常 / IsPending の分岐
    //Collections11版はWouldOverflowMutableがtrueのときelseブロックなしでThrowLogFullを呼ぶ
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Indexer_set() {
        //WouldOverflowMutable=true → OutOfMemoryException
        {
            using var store = new DictionaryStore<int,int>();
            using var dict = store.Create();
            Assert.Throws<OutOfMemoryException>(() => {
                for(var a = 0; a<10000; a++) {
                    dict[a]=a;
                }
            });
        }
        //正常: 新規設定と上書き (WouldOverflowMutable=false, IsPending=false パス)
        {
            using var store = new DictionaryStore<int,string>();
            using var dict = store.Create();
            dict[1]="hello";
            Assert.Equal("hello",dict[1]);
            dict[1]="world";
            Assert.Equal("world",dict[1]);
        }
        //IsPending: デバイスを使いMutableFraction=0で読み取り専用領域が発生する状況を作る
        {
            var folder = CreateFolder();
            using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
            using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: TestMemorySize,MutableFraction: 0);
            using var dict = store.Create();
            for(var a = 0; a<500; a++) {
                dict[a]=$"v{a}";
            }
            //読み取り専用領域のキーをUpsertするとIsPendingが発生する可能性がある
            dict[0]="updated";
            Assert.Equal("updated",dict[0]);
        }
    }
    //Dictionary.ContainsKey: IsPending分岐 (Collections11版はContainsKeyにもIsPendingチェックがある)
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void ContainsKey_IsPending() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<500; a++) {
            dict.Add(a,$"v{a}");
        }
        //読み取り専用領域のキーをContainsKey (IsPending経路)
        Assert.True(dict.ContainsKey(0));
        Assert.False(dict.ContainsKey(9999));
    }
}
