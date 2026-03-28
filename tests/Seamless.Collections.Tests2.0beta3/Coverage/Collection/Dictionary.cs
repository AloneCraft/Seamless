using Common;
using System.Collections;
using System.Runtime.Serialization;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// src/Seamless.Collections/Collection/Dictionary.cs の全分岐をカバーアする。
/// 既存テスト(CoverageDictionary等)でカバーしていない分岐に注力する。
/// </summary>
[Collection("Sequential")]
public class Dictionary {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    [Fact, Trait("Category","Coverage")]
    public void IsReadOnly() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.False(dict.IsReadOnly);
    }

    [Fact, Trait("Category","Coverage")]
    public void Contains_KeyValuePair() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");

        Assert.Contains(new KeyValuePair<int,string>(1,"a"),dict);
        Assert.DoesNotContain(new KeyValuePair<int,string>(1,"x"),dict);
        Assert.DoesNotContain(new KeyValuePair<int,string>(999,"a"),dict);
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

    [Fact, Trait("Category","Coverage")]
    public void ToList() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");

        var list = dict.ToList();
        Assert.Equal(2,list.Count);
        Assert.Contains(list,x => x.Key==1&&x.Value=="a");
        Assert.Contains(list,x => x.Key==2&&x.Value=="b");
    }

    [Fact, Trait("Category","Coverage")]
    public void Keys() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        var expected = new KeyValuePair<int,string>(1,"a");
        dict.Add(expected);
        var Keys = dict.Keys;
        var actual = Keys.Single();
        Assert.Equal(expected.Key,actual);
    }
    [Fact, Trait("Category","Coverage")]
    public void Values() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        var expected = new KeyValuePair<int,string>(1,"a");
        dict.Add(expected);
        var Values = dict.Values;
        var actual = Values.Single();
        Assert.Equal(expected.Value,actual);
    }
    [Fact, Trait("Category","Coverage")]
    public void Add_key_value() {
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
        //Success (implicit: switchにcaseなし)
        {
            using var store = new DictionaryStore<int,string>();
            using var dict = store.Create();
            dict.Add(99,"v99");
            Assert.Equal("v99",dict[99]);
        }
    }
    [Fact, Trait("Category","Coverage")]
    public void Add_item() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        var expected = new KeyValuePair<int,string>(1,"a");
        dict.Add(expected);
        var actual = dict.Single();
        var ex = Assert.Throws<ArgumentException>(() => dict.Add(1,"b"));
        Assert.Equal(expected,actual);
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_key() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();

        dict.Add(1,"a");

        Assert.True(dict.Remove(1,out var removedValue));
        Assert.Equal("a",removedValue);
        Assert.False(dict.ContainsKey(1));

        Assert.False(dict.Remove(1,out var missingValue));
        Assert.Null(missingValue);
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_item() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();

        var collection = (ICollection<KeyValuePair<int,string>>)dict;

        Assert.False(collection.Remove(new KeyValuePair<int,string>(99,"x")));

        dict.Add(1,"a");

        Assert.False(collection.Remove(new KeyValuePair<int,string>(1,"different")));
        Assert.True(dict.ContainsKey(1));

        Assert.True(collection.Remove(new KeyValuePair<int,string>(1,"a")));
        Assert.False(dict.ContainsKey(1));
    }
    //Dictionary.CopyTo: null/負数/配列小/正常 の4分岐
    [Fact, Trait("Category","Coverage")]
    public void CopyTo() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        //null → ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(null!,0));
        //負のインデックス → ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(new KeyValuePair<int,string>[2],-1));
        //配列が小さすぎる → ArgumentException
        Assert.Throws<ArgumentException>(() => ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(new KeyValuePair<int,string>[1],0));
        //正常
        {
            var array = new KeyValuePair<int,string>[3];
            ((ICollection<KeyValuePair<int,string>>)dict).CopyTo(array,1);
            Assert.Equal(2,array.Count(x => x.Value!=null));
        }
    }
    //Dictionary.this[TKey] getter: TryGetValue→true / TryGetValue→false の2分岐
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
    //Dictionary.this[TKey] setter: WouldOverflowMutable=true / 正常 / Status.IsPending の3分岐
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
            using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0.1);
            using var dict = store.Create();
            for(var a = 0; a<1200; a++) {
                dict[a]=$"v{a}";
            }
            store.CompactWithLookup();
            //読み取り専用・退避済みの可能性があるキー群を更新してIsPending経路を狙う
            for(var a = 0; a<128; a++) {
                dict[a]=$"updated{a}";
            }
            dict[0]="updated";
            Assert.Equal("updated",dict[0]);
        }
    }
    [Fact, Trait("Category","Coverage")]
    public void IReadOnlyDictionary_Keys() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        IReadOnlyDictionary<int,string> readOnly = dict;
        var keys = readOnly.Keys;
        Assert.Equal(2,keys.Count());
        Assert.Contains(1,keys);
        Assert.Contains(2,keys);
    }
    [Fact, Trait("Category","Coverage")]
    public void IReadOnlyDictionary_Values() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        IReadOnlyDictionary<int,string> readOnly = dict;
        var values = readOnly.Values;
        Assert.Equal(2,values.Count());
        Assert.Contains("a",values);
        Assert.Contains("b",values);
    }
}
