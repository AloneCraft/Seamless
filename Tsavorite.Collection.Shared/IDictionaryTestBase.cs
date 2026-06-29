using Seamless.Tsavorite.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Tsavorite.core;
using Seamless.Collections;
namespace Tsavorite.Collection;
/// <summary>
/// <see cref="global::System.Collections.Generic.IDictionary{TKey,TValue}"/> 蜈ｱ騾壹ユ繧ｹ繝亥渕蠎輔け繝ｩ繧ｹ縲・/// 霑ｽ蜉縺ｮ Tsavorite 蝗ｺ譛画､懆ｨｼ縺ｯ <see cref="DictionaryTestBase{TStrategy,TKey,TValue}"/> 縺ｧ謇ｱ縺・・/// </summary>
public abstract class IDictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private string CreateDiskFolder() =>
        TestPath.Directory(this);
    [BenchFact,Benchmark]
    public void IsReadOnly() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            Assert.False(dict.IsReadOnly);
        }
    }
    [BenchFact]
    public void Add() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩・ｧ驍ｵ・ｺ鬮ｦ・ｪ繝ｻ迢暦ｽｸ・ｺ髦ｮ蜷ｮ繝ｻ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1),TStrategy.V(2)));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [BenchFact,Benchmark]
    public void Add_KeyValuePair() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩・ｧ驍ｵ・ｺ鬮ｦ・ｪ繝ｻ迢暦ｽｸ・ｺ髦ｮ蜷ｮ繝ｻ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(5),TStrategy.V(5)));
            Assert.True(dict.ContainsKey(TStrategy.K(5)));
        }
    }
    [BenchFact,Benchmark]
    public void Contains() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩・ｯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong())));
        }
    }
    [BenchFact]
    public void CopyTo() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
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
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩・ｯ髣懃§蜚ｱ繝ｻ・､郢晢ｽｻ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[2],-1));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩貊難ｽｦ鄙ｫ繝ｻ驍ｵ・ｺ隰疲ｻゑｽｽ・ｰ闕ｳ螂・ｽｼ繝ｻ・ｸ・ｺ陷ｷ・ｶ驍・・・ｹ・ｧ闕ｵ譏ｶ繝ｻ髣懃§蜚ｱ繝ｻ・､郢晢ｽｻ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(2),TStrategy.V(2));
            Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[1],0));
        }
    }
    [BenchFact,Benchmark]
    public void ToList() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        var list = dict.ToList();
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            Assert.Equal(2,list.Count);
        }
    }
    [BenchFact]
    public void Keys() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            Assert.True(new global::System.Collections.Generic.HashSet<TKey>(new[] { TStrategy.K(1),TStrategy.K(2) }).SetEquals(dict.Keys));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            Assert.True(dict.Keys.IsReadOnly);
            Assert.IsNotType<List<TKey>>(dict.Keys);
            Assert.Throws<NotSupportedException>(() => dict.Keys.Add(TStrategy.K(3)));
        }
    }
    [BenchFact]
    public void Values() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            Assert.True(new global::System.Collections.Generic.HashSet<TValue>(new[] { TStrategy.V(1),TStrategy.V(2) }).SetEquals(dict.Values));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            Assert.True(dict.Values.IsReadOnly);
            Assert.IsNotType<List<TValue>>(dict.Values);
            Assert.Throws<NotSupportedException>(() => dict.Values.Add(TStrategy.V(3)));
        }
    }
    [BenchFact,Benchmark]
    public void ContainsKey() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.ContainsKey(TStrategy.K(999)));
        }
    }
    [BenchFact,Benchmark]
    public void TryGetValue() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(3),TStrategy.V(3));
            Assert.True(dict.TryGetValue(TStrategy.K(3),out var val));
            Assert.Equal(TStrategy.V(3),val);
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.TryGetValue(TStrategy.K(0),out var val));
            Assert.Equal(default,val);
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.TryGetValue(TStrategy.K(1),out var v));
            Assert.Equal(TStrategy.V(1),v);
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
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
    [BenchFact]
    public void Item() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict[TStrategy.K(10)]=TStrategy.V(10);
            Assert.True(dict.TryGetValue(TStrategy.K(10),out var v));
            Assert.Equal(TStrategy.V(10),v);
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<KeyNotFoundException>(() => {
                var _ = dict[TStrategy.K(999)];
            });
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict[TStrategy.K(i)]=TStrategy.V(i);
            }));
        }
    }
    [BenchFact,Benchmark]
    public void Remove() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Remove(TStrategy.K(99)));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
        }
    }
    [BenchFact,Benchmark]
    public void Remove_KeyValuePair() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
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
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.VWrong()));
            Assert.False(removed);
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong()));
            Assert.False(removed);
        }
    }
    [BenchFact,Benchmark]
    public void Clear() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            dict.Clear();
            Assert.Empty(dict);
        }
    }
    [BenchFact,Benchmark]
    public void Count() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩驍ｵ・ｺ陷ｷ・ｶ繝ｻ迢暦ｽｸ・ｺ髦ｮ蜷ｮ繝ｻ
            Assert.Equal(2,dict.Count);
        }
    }
    [BenchFact,Benchmark]
    public void GetEnumerator() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩・ｧ髯具ｽｻ驍・ｽｲ髮匁ｪ趣ｽｸ・ｺ繝ｻ・ｧ驍ｵ・ｺ鬮ｦ・ｪ繝ｻ迢暦ｽｸ・ｺ髦ｮ蜷ｮ繝ｻ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable<KeyValuePair<TKey,TValue>> e = dict;
            Assert.Single(e);
        }
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩・ｧ髯具ｽｻ驍・ｽｲ髮匁ｪ趣ｽｸ・ｺ繝ｻ・ｧ驍ｵ・ｺ鬮ｦ・ｪ繝ｻ迢暦ｽｸ・ｺ髦ｮ蜷ｮ繝ｻ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            global::System.Collections.IEnumerable e = dict;
            var count = 0;
            var en = e.GetEnumerator();
            while(en.MoveNext()) count++;
            Assert.Equal(1,count);
        }
    }
    [BenchFact]
    [SuppressMessage("Interoperability","CA1416:Validate platform compatibility",Justification = "Cross-platform test fixture")]
    public void WouldOverflowMutable() {
        {
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
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
            // 繧ｳ繝｡繝ｳ繝域枚蟄怜喧縺台ｿｮ豁｣貂医∩
            using var sh = TStrategy.CreateMemoryStore(memorySize: global::Seamless.Tsavorite.Const.LogMemorySize,mutableFraction: 0.5);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [BenchFact,Benchmark]
    [SuppressMessage("Interoperability","CA1416:Validate platform compatibility",Justification = "Cross-platform test fixture")]
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
