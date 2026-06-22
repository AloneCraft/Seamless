using Seamless.Collections.Tsavorite;
using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Test.Seamless.Collections;
/// <summary>
/// <see cref="global::Seamless.Collections.Tsavorite.IDictionary{TKey,TValue}"/> 固有のテスト基底クラス。
/// <see cref="global::System.Collections.Generic.IDictionary{TKey,TValue}"/> 共通テストは <see cref="IDictionaryTestBase{TStrategy,TKey,TValue}"/> に集約されており、
/// このクラスでは追加で TryAdd、Remove(out value)、Dispose、EntryCount を確認する。
/// </summary>
public abstract class DictionaryTestBase<TStrategy, TKey, TValue>:IDictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private string CreateDiskFolder() =>
        TestPath.Directory(this);
    [Fact, Trait("Category","Unit")]
    public void TryAdd() {
        {
            // 新規キーの追加は Success
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TryAddResult.Success,result);
        }
        {
            // 重複キーの追加は KeyAlreadyExists
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(2));
            Assert.Equal(TryAddResult.KeyAlreadyExists,result);
        }
        {
            // コールドデータ読み込み後の重複追加でも KeyAlreadyExists
            var folder = this.CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(TStrategy.K(0),TStrategy.V(1)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_key_value() {
        {
            // 存在するキーは削除値を返す
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(TStrategy.K(1),out var value);
            Assert.True(removed);
            Assert.Equal(TStrategy.V(1),value);
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 存在しないキーは false と default を返す
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var removed = dict.Remove(TStrategy.K(99),out var value);
            Assert.False(removed);
            Assert.Equal(default,value);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose() {
        var sh = TStrategy.CreateMemoryStore();
        var dict = sh.NewDict();
        {
            // Dispose は複数回呼んでも安全なこと
            Assert.False(dict.IsDisposed);
            dict.Dispose();
            Assert.True(dict.IsDisposed);
            dict.Dispose();
            sh.Dispose();
        }
    }
    [Fact, Trait("Category","Unit")]
    public void EntryCount() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        {
            // ストア上の件数が 1 以上であること
            Assert.True(dict.EntryCount>=1);
        }
    }
}
