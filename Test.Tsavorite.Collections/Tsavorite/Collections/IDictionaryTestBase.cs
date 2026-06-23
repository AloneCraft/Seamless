using Seamless.Tsavorite.Collections;
using global::System.Diagnostics.CodeAnalysis;
using System.Linq;
using Tsavorite.core;
using Test.Seamless.Collections;
namespace Test.Seamless.Collections;
/// <summary>
/// <see cref="IDictionary{TKey,TValue}"/> インターフェース経由のテスト基底クラス。
/// <see cref="DictionaryTestBase{TStrategy,TKey,TValue}"/> はこのクラスを利用し、
/// <see cref="global::Seamless.Tsavorite.Collections.IDictionary{TKey,TValue}"/> 固有のテストを追加する。
/// </summary>
public abstract class IDictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private string CreateDiskFolder() =>
        TestPath.Directory(this);
    [Fact, Trait("Category","Unit")]
    public void IsReadOnly() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        {
            // 読み取り専用ではないこと
            Assert.False(dict.IsReadOnly);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add() {
        {
            // 追加できること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 重複キーでは例外になること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1),TStrategy.V(2)));
        }
        {
            // ログが満杯になると例外になること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add_KeyValuePair() {
        {
            // KeyValuePair で追加できること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(5),TStrategy.V(5)));
            Assert.True(dict.ContainsKey(TStrategy.K(5)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Contains() {
        {
            // キーと値が一致する要素は true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
        }
        {
            // 不一致要素は false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong())));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo() {
        {
            // 全要素を配列にコピーできること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(10),TStrategy.V(10));
            var array = new KeyValuePair<TKey,TValue>[2];
            dict.CopyTo(array,0);
            Assert.Equal(2,array.Length);
        }
        {
            // null 配列は例外
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
        }
        {
            // 負のインデックスは例外
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[2],-1));
        }
        {
            // 配列が小さすぎると例外
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(2),TStrategy.V(2));
            Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[1],0));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ToList() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        var list = dict.ToList();
        {
            // 全エントリを返すこと
            Assert.Equal(2,list.Count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Keys() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // キー一覧を取得できること
            Assert.True(new global::System.Collections.Generic.HashSet<TKey>(new[] { TStrategy.K(1),TStrategy.K(2) }).SetEquals(dict.Keys));
        }
        {
            // キー一覧は全件コピー済みの標準Listではなく読み取り専用viewであること
            Assert.True(dict.Keys.IsReadOnly);
            Assert.IsNotType<List<TKey>>(dict.Keys);
            Assert.Throws<NotSupportedException>(() => dict.Keys.Add(TStrategy.K(3)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Values() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 値一覧を取得できること
            Assert.True(new global::System.Collections.Generic.HashSet<TValue>(new[] { TStrategy.V(1),TStrategy.V(2) }).SetEquals(dict.Values));
        }
        {
            // 値一覧は全件コピー済みの標準Listではなく読み取り専用viewであること
            Assert.True(dict.Values.IsReadOnly);
            Assert.IsNotType<List<TValue>>(dict.Values);
            Assert.Throws<NotSupportedException>(() => dict.Values.Add(TStrategy.V(3)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ContainsKey() {
        {
            // 存在するキーは true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 存在しないキーは false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.ContainsKey(TStrategy.K(999)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void TryGetValue() {
        {
            // キーが存在する時のテスト
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(3),TStrategy.V(3));
            Assert.True(dict.TryGetValue(TStrategy.K(3),out var val));
            Assert.Equal(TStrategy.V(3),val);
        }
        {
            // キーが存在しない時のテスト
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.TryGetValue(TStrategy.K(0),out var val));
            Assert.Equal(default,val);
        }
        {
            // 追加直後でも読み出せること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.TryGetValue(TStrategy.K(1),out var v));
            Assert.Equal(TStrategy.V(1),v);
        }
        {
            // FlushAndEvict 後もコールドデータを読めること
            var folder = this.CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.True(dict.TryGetValue(TStrategy.K(0),out var v));
            Assert.Equal(TStrategy.V(0),v);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Item() {
        {
            // setter で値を設定できること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict[TStrategy.K(10)]=TStrategy.V(10);
            Assert.True(dict.TryGetValue(TStrategy.K(10),out var v));
            Assert.Equal(TStrategy.V(10),v);
        }
        {
            // getter で不存在キー参照時は例外
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<KeyNotFoundException>(() => {
                var _ = dict[TStrategy.K(999)];
            });
        }
        {
            // ログ満杯時の setter は例外
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict[TStrategy.K(i)]=TStrategy.V(i);
            }));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove() {
        {
            // 存在キーの削除に成功すること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 存在しないキーは false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Remove(TStrategy.K(99)));
        }
        {
            // Write パスの削除に成功すること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_KeyValuePair() {
        {
            // キーと値が一致すると削除される
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
            foreach(var _ in dict) { }
            Assert.True(removed);
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 値が不一致なら削除されない
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.VWrong()));
            Assert.False(removed);
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // キーが存在しなければ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong()));
            Assert.False(removed);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Clear() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // クリア後は件数が 0 になること
            dict.Clear();
            Assert.Empty(dict);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Count() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 追加数と一致すること
            Assert.Equal(2,dict.Count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void GetEnumerator() {
        {
            // IEnumerable<T> 経由で列挙できること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable<KeyValuePair<TKey,TValue>> e = dict;
            Assert.Single(e);
        }
        {
            // IEnumerable 経由で列挙できること
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable e = dict;
            var count = 0;
            var en = e.GetEnumerator();
            while(en.MoveNext()) count++;
            Assert.Equal(1,count);
        }
    }
    [Fact, Trait("Category","Unit")]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<保留中>")]
    public void WouldOverflowMutable() {
        {
            // mutableFraction=0 でも格納できること
            var folder = this.CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice,memorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,mutableFraction: 0);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TStrategy.V(1),dict[TStrategy.K(1)]);
        }
        {
            // mutable 領域を使い切ると OutOfMemoryException
            using var sh = TStrategy.CreateMemoryStore(memorySize: global::Seamless.Tsavorite.Const.LogMemorySize,mutableFraction: 0.5);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [Fact, Trait("Category","Unit")]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<保留中>")]
    public void SaveLoad() {
        var folder = this.CreateDiskFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice,logObjDevice,checkpointDir: checkpointDir);
            using var rawSet = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> set = rawSet;
            set[TStrategy.K(1)]=TStrategy.V(1);
            set[TStrategy.K(2)]=TStrategy.V(2);
            set[TStrategy.K(3)]=TStrategy.V(3);
            Assert.Equal(3,set.Count);
            Assert.True(set.ContainsKey(TStrategy.K(1)));
            token=sh.Save();
            Assert.True(set.ContainsKey(TStrategy.K(1)));
        }
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice,logObjDevice,checkpointDir: checkpointDir,recoverDevice: true);
            sh.Load(token);
            var (begin2,head2,ro2,tail2)=sh.Address;
            using var rawSet = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> set = rawSet;
            Assert.True(set.ContainsKey(TStrategy.K(1)),$"ContainsKey(K(1)) failed. Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.ContainsKey(TStrategy.K(2)));
            Assert.True(set.ContainsKey(TStrategy.K(3)));
            Assert.False(set.ContainsKey(TStrategy.K(99)));
        }
    }
}
