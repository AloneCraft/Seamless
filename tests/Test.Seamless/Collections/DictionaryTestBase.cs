using Seamless;
using Seamless.Collections;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

using Tsavorite.core;

namespace Test.Seamless.Collections;

public abstract class DictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    private static string CreateDiskFolder() =>
        Path.Combine(Path.GetTempPath(), "SeamlessTest_" + Guid.NewGuid().ToString("N"));

    [Fact, Trait("Category", "Unit")]
    public void IsReadOnly_偽を返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.False(dict.IsReadOnly);
    }

    [Fact, Trait("Category", "Unit")]
    public void TryAdd_新規キー_Successを返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        var result = dict.TryAdd(TStrategy.K(1), TStrategy.V(1));
        Assert.Equal(TryAddResult.Success, result);
    }

    [Fact, Trait("Category", "Unit")]
    public void TryAdd_重複キー_KeyAlreadyExistsを返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        var result = dict.TryAdd(TStrategy.K(1), TStrategy.V(2));
        Assert.Equal(TryAddResult.KeyAlreadyExists, result);
    }

    [Fact, Trait("Category", "Unit")]
    public void Add_KVP_正常追加() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(new KeyValuePair<TKey, TValue>(TStrategy.K(5), TStrategy.V(5)));
        Assert.True(dict.ContainsKey(TStrategy.K(5)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Add_重複キー_例外を投げる() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1), TStrategy.V(2)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Contains_KVP_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        ICollection<KeyValuePair<TKey, TValue>> c = dict;
        Assert.True(c.Contains(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1))));
    }

    [Fact, Trait("Category", "Unit")]
    public void Contains_KVP_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        ICollection<KeyValuePair<TKey, TValue>> c = dict;
        Assert.False(c.Contains(new KeyValuePair<TKey, TValue>(TStrategy.K(99), TStrategy.VWrong())));
    }

    [Fact, Trait("Category", "Unit")]
    public void ContainsKey_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.True(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void ContainsKey_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.False(dict.ContainsKey(TStrategy.K(999)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_KVP_値一致_削除成功() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        var removed = dict.Remove(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.V(1)));
        foreach (var _ in dict) { }
        Assert.True(removed);
        Assert.False(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_KVP_値不一致_削除しない() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        var removed = dict.Remove(new KeyValuePair<TKey, TValue>(TStrategy.K(1), TStrategy.VWrong()));
        Assert.False(removed);
        Assert.True(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_KVP_キー不存在_偽を返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        var removed = dict.Remove(new KeyValuePair<TKey, TValue>(TStrategy.K(99), TStrategy.VWrong()));
        Assert.False(removed);
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_out_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        var removed = dict.Remove(TStrategy.K(1), out var value);
        Assert.True(removed);
        Assert.Equal(TStrategy.V(1), value);
        Assert.False(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_out_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        var removed = dict.Remove(TStrategy.K(99), out var value);
        Assert.False(removed);
        Assert.Equal(default, value);
    }

    [Fact, Trait("Category", "Unit")]
    public void CopyTo_正常() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(10), TStrategy.V(10));
        var array = new KeyValuePair<TKey, TValue>[2];
        dict.CopyTo(array, 0);
        Assert.Equal(2, array.Length);
    }

    [Fact, Trait("Category", "Unit")]
    public void CopyTo_null配列_例外() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!, 0));
    }

    [Fact, Trait("Category", "Unit")]
    public void CopyTo_負のインデックス_例外() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<TKey, TValue>[2], -1));
    }

    [Fact, Trait("Category", "Unit")]
    public void CopyTo_配列が小さすぎる_例外() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));
        Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<TKey, TValue>[1], 0));
    }

    [Fact, Trait("Category", "Unit")]
    public void ToList_全エントリを返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));
        var list = dict.ToList();
        Assert.Equal(2, list.Count);
    }

    [Fact, Trait("Category", "Unit")]
    public void Item_Setter_値を設定する() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict[TStrategy.K(10)] = TStrategy.V(10);
        Assert.True(dict.TryGetValue(TStrategy.K(10), out var v));
        Assert.Equal(TStrategy.V(10), v);
    }

    [Fact, Trait("Category", "Unit")]
    public void Item_Getter_存在しないキー_例外() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.Throws<KeyNotFoundException>(() => { var _ = dict[TStrategy.K(999)]; });
    }

    [Fact, Trait("Category", "Unit")]
    public void TryGetValue_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(3), TStrategy.V(3));
        Assert.True(dict.TryGetValue(TStrategy.K(3), out var val));
        Assert.Equal(TStrategy.V(3), val);
    }

    [Fact, Trait("Category", "Unit")]
    public void TryGetValue_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.False(dict.TryGetValue(TStrategy.K(0), out var val));
        Assert.Equal(default, val);
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.True(dict.Remove(TStrategy.K(1)));
        Assert.False(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.False(dict.Remove(TStrategy.K(99)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Clear_後Count0() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));
        dict.Clear();
        Assert.Equal(0, dict.Count);
    }

    [Fact, Trait("Category", "Unit")]
    public void Count_件数を返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));
        Assert.Equal(2, dict.Count);
    }

    [Fact, Trait("Category", "Unit")]
    public void GetEnumerator_IEnumerableT() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        IEnumerable<KeyValuePair<TKey, TValue>> e = dict;
        Assert.Single(e);
    }

    [Fact, Trait("Category", "Unit")]
    public void Add_正常追加() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.True(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Dispose_安全に呼び出せる() {
        var sh = TStrategy.CreateMemoryStore();
        var dict = sh.NewDict();
        Assert.False(dict.IsDisposed);
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        dict.Dispose();
        sh.Dispose();
    }

    [Fact, Trait("Category", "Unit")]
    public void GetEnumerator_IEnumerable() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        IEnumerable e = dict;
        var count = 0;
        var en = e.GetEnumerator();
        while (en.MoveNext()) count++;
        Assert.Equal(1, count);
    }

    [Fact, Trait("Category", "Unit")]
    public void Add_LogFull_例外を投げる() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.Throws<OutOfMemoryException>((Action)(() => {
            for (var i = 0; ; i++) dict.Add(TStrategy.K(i), TStrategy.V(i));
        }));
    }

    [Fact, Trait("Category", "Unit")]
    public void Item_Setter_LogFull_例外を投げる() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        Assert.Throws<OutOfMemoryException>((Action)(() => {
            for (var i = 0; ; i++) dict[TStrategy.K(i)] = TStrategy.V(i);
        }));
    }

    [Fact, Trait("Category", "Unit")]
    public void EntryCount_件数を返す() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.True(dict.EntryCount >= 1);
    }

    [Fact, Trait("Category", "Unit")]
    public void Pending_Writeパス() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.True(dict.Remove(TStrategy.K(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Pending_Readパス() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.True(dict.TryGetValue(TStrategy.K(1), out var v));
        Assert.Equal(TStrategy.V(1), v);
    }

    [Fact, Trait("Category", "Unit")]
    [SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性を検証", Justification = "<保留中>")]
    public void WouldOverflowMutable_MutableFractionZero() {
        var folder = CreateDiskFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder, "log.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder + "_obj", "obj.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var sh = TStrategy.CreateStorageStore(logDevice, objDevice, memorySize: Const.MinObjectLogCapacity, mutableFraction: 0);
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        Assert.Equal(TStrategy.V(1), dict[TStrategy.K(1)]);
    }

    [Fact, Trait("Category", "Unit")]
    public void WouldOverflowMutable_OverflowDetected() {
        using var sh = TStrategy.CreateMemoryStore(memorySize: Const.LogMemorySize, mutableFraction: 0.5);
        using var dict = sh.NewDict();
        Assert.Throws<OutOfMemoryException>((Action)(() => {
            for (var i = 0; ; i++) dict.Add(TStrategy.K(i), TStrategy.V(i));
        }));
    }

    [Fact, Trait("Category", "Unit")]
    [SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性を検証", Justification = "<保留中>")]
    public void Pending_Readパス_IsPending() {
        var folder = CreateDiskFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder, "log.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder + "_obj", "obj.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var sh = TStrategy.CreateStorageStore(logDevice, objDevice);
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(0), TStrategy.V(0));
        sh.FlushAndEvict(true);
        Assert.True(dict.TryGetValue(TStrategy.K(0), out var v));
        Assert.Equal(TStrategy.V(0), v);
    }

    [Fact, Trait("Category", "Unit")]
    [SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性を検証", Justification = "<保留中>")]
    public void Pending_Writeパス_IsPending() {
        var folder = CreateDiskFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder, "log.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder + "_obj", "obj.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var sh = TStrategy.CreateStorageStore(logDevice, objDevice);
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(0), TStrategy.V(0));
        sh.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists, dict.TryAdd(TStrategy.K(0), TStrategy.V(1)));
    }

    [Fact, Trait("Category", "Unit")]
    public void Keys() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));
        Assert.Equivalent(new[] { TStrategy.K(1), TStrategy.K(2) }, dict.Keys);
    }

    [Fact, Trait("Category", "Unit")]
    public void Values() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1), TStrategy.V(1));
        dict.Add(TStrategy.K(2), TStrategy.V(2));
        Assert.Equivalent(new[] { TStrategy.V(1), TStrategy.V(2) }, dict.Values);
    }

    [Fact, Trait("Category", "Unit")]
    [SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性を検証", Justification = "<保留中>")]
    public void SaveLoad() {
        var folder = CreateDiskFolder();
        var checkpointDir = folder + "_checkpoint";
        Guid token;
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder + "_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice, logObjDevice, checkpointDir: checkpointDir);
            using var set = sh.NewDict();
            set[TStrategy.K(1)] = TStrategy.V(1);
            set[TStrategy.K(2)] = TStrategy.V(2);
            set[TStrategy.K(3)] = TStrategy.V(3);
            Assert.Equal(3, set.Count);
            Assert.True(set.ContainsKey(TStrategy.K(1)));
            token = sh.Save();
            Assert.True(set.ContainsKey(TStrategy.K(1)));
        }
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder + "_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice, logObjDevice, checkpointDir: checkpointDir, recoverDevice: true);
            sh.Load(token);
            var (begin2, head2, ro2, tail2) = sh.Address;
            using var set = sh.NewDict();
            Assert.True(set.ContainsKey(TStrategy.K(1)), $"ContainsKey(K(1)) failed. EntryCount={set.EntryCount}, Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.ContainsKey(TStrategy.K(2)));
            Assert.True(set.ContainsKey(TStrategy.K(3)));
            Assert.False(set.ContainsKey(TStrategy.K(99)));
        }
    }
}
