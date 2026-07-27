using Tsavorite.core;
namespace Shared.Tsavorite.Collections;
/// <summary>
/// IDictionaryの動作を検証する。
public abstract class DictionaryTestBase<TStrategy, TKey, TValue>:IDictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private string CreateDiskFolder() =>
        TestPath.Directory(this);
    [BenchFact, Benchmark]
    public void TryAdd() {
        {
            // 新規キーの追加は Success
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TryAddResult.Success,result);
        }
        {
            // Dictionaryの動作を検証する。
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(2));
            Assert.Equal(TryAddResult.KeyAlreadyExists,result);
        }
        {
            // Dictionaryの動作を検証する。
            var folder = this.CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(TStrategy.K(0),TStrategy.V(1)));
        }
    }
    [BenchFact, Benchmark]
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
            // Dictionaryの動作を検証する。
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var removed = dict.Remove(TStrategy.K(99),out var value);
            Assert.False(removed);
            Assert.Equal(default,value);
        }
    }
    [BenchFact, Benchmark]
    public void Dispose() {
        var sh = TStrategy.CreateMemoryStore();
        var dict = sh.NewDict();
        {
            // Dictionaryの動作を検証する。
            Assert.False(dict.IsDisposed);
            dict.Dispose();
            Assert.True(dict.IsDisposed);
            dict.Dispose();
            sh.Dispose();
        }
    }
    [BenchFact, Benchmark]
    public void EntryCount() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        {
            // Dictionaryの動作を検証する。
            Assert.True(dict.EntryCount>=1);
        }
    }
}
