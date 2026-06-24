using Seamless.Tsavorite.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Tsavorite.core;
using Test.Collections;
namespace Tsavorite.Collection.Shared;
/// <summary>
/// <see cref="IDictionary{TKey,TValue}"/> 繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ邨檎罰縺ｮ繝・せ繝亥渕蠎輔け繝ｩ繧ｹ縲・/// <see cref="DictionaryTestBase{TStrategy,TKey,TValue}"/> 縺ｯ縺薙・繧ｯ繝ｩ繧ｹ繧貞茜逕ｨ縺励・/// <see cref="global::Seamless.Tsavorite.Collections.IDictionary{TKey,TValue}"/> 蝗ｺ譛峨・繝・せ繝医ｒ霑ｽ蜉縺吶ｋ縲・/// </summary>
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
            // 隱ｭ縺ｿ蜿悶ｊ蟆ら畑縺ｧ縺ｯ縺ｪ縺・％縺ｨ
            Assert.False(dict.IsReadOnly);
        }
    }
    [Xunit.Fact]
    public void Add() {
        {
            // 霑ｽ蜉縺ｧ縺阪ｋ縺薙→
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 驥崎､・く繝ｼ縺ｧ縺ｯ萓句､悶↓縺ｪ繧九％縺ｨ
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1),TStrategy.V(2)));
        }
        {
            // 繝ｭ繧ｰ縺梧ｺ譚ｯ縺ｫ縺ｪ繧九→萓句､悶↓縺ｪ繧九％縺ｨ
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
            // KeyValuePair 縺ｧ霑ｽ蜉縺ｧ縺阪ｋ縺薙→
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
            // 繧ｭ繝ｼ縺ｨ蛟､縺御ｸ閾ｴ縺吶ｋ隕∫ｴ縺ｯ true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
        }
        {
            // 荳堺ｸ閾ｴ隕∫ｴ縺ｯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong())));
        }
    }
    [Xunit.Fact]
    public void CopyTo() {
        {
            // 蜈ｨ隕∫ｴ繧帝・蛻励↓繧ｳ繝斐・縺ｧ縺阪ｋ縺薙→
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
            // null 驟榊・縺ｯ萓句､・
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
        }
        {
            // 雋縺ｮ繧､繝ｳ繝・ャ繧ｯ繧ｹ縺ｯ萓句､・
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[2],-1));
        }
        {
            // 驟榊・縺悟ｰ上＆縺吶℃繧九→萓句､・
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
            // 蜈ｨ繧ｨ繝ｳ繝医Μ繧定ｿ斐☆縺薙→
            Assert.Equal(2,list.Count);
        }
    }
    [Xunit.Fact]
    public void Keys() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 繧ｭ繝ｼ荳隕ｧ繧貞叙蠕励〒縺阪ｋ縺薙→
            Assert.True(new global::System.Collections.Generic.HashSet<TKey>(new[] { TStrategy.K(1),TStrategy.K(2) }).SetEquals(dict.Keys));
        }
        {
            // 繧ｭ繝ｼ荳隕ｧ縺ｯ蜈ｨ莉ｶ繧ｳ繝斐・貂医∩縺ｮ讓呎ｺ豊ist縺ｧ縺ｯ縺ｪ縺剰ｪｭ縺ｿ蜿悶ｊ蟆ら畑view縺ｧ縺ゅｋ縺薙→
            Assert.True(dict.Keys.IsReadOnly);
            Assert.IsNotType<List<TKey>>(dict.Keys);
            Assert.Throws<NotSupportedException>(() => dict.Keys.Add(TStrategy.K(3)));
        }
    }
    [Xunit.Fact]
    public void Values() {
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // 蛟､荳隕ｧ繧貞叙蠕励〒縺阪ｋ縺薙→
            Assert.True(new global::System.Collections.Generic.HashSet<TValue>(new[] { TStrategy.V(1),TStrategy.V(2) }).SetEquals(dict.Values));
        }
        {
            // 蛟､荳隕ｧ縺ｯ蜈ｨ莉ｶ繧ｳ繝斐・貂医∩縺ｮ讓呎ｺ豊ist縺ｧ縺ｯ縺ｪ縺剰ｪｭ縺ｿ蜿悶ｊ蟆ら畑view縺ｧ縺ゅｋ縺薙→
            Assert.True(dict.Values.IsReadOnly);
            Assert.IsNotType<List<TValue>>(dict.Values);
            Assert.Throws<NotSupportedException>(() => dict.Values.Add(TStrategy.V(3)));
        }
    }
    [BenchFact,Benchmark]
    public void ContainsKey() {
        {
            // 蟄伜惠縺吶ｋ繧ｭ繝ｼ縺ｯ true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 蟄伜惠縺励↑縺・く繝ｼ縺ｯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.ContainsKey(TStrategy.K(999)));
        }
    }
    [BenchFact,Benchmark]
    public void TryGetValue() {
        {
            // 繧ｭ繝ｼ縺悟ｭ伜惠縺吶ｋ譎ゅ・繝・せ繝・
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(3),TStrategy.V(3));
            Assert.True(dict.TryGetValue(TStrategy.K(3),out var val));
            Assert.Equal(TStrategy.V(3),val);
        }
        {
            // 繧ｭ繝ｼ縺悟ｭ伜惠縺励↑縺・凾縺ｮ繝・せ繝・
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.TryGetValue(TStrategy.K(0),out var val));
            Assert.Equal(default,val);
        }
        {
            // 霑ｽ蜉逶ｴ蠕後〒繧りｪｭ縺ｿ蜃ｺ縺帙ｋ縺薙→
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.TryGetValue(TStrategy.K(1),out var v));
            Assert.Equal(TStrategy.V(1),v);
        }
        {
            // FlushAndEvict 蠕後ｂ繧ｳ繝ｼ繝ｫ繝峨ョ繝ｼ繧ｿ繧定ｪｭ繧√ｋ縺薙→
            var folder = CreateDiskFolder();
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
    [Xunit.Fact]
    public void Item() {
        {
            // setter 縺ｧ蛟､繧定ｨｭ螳壹〒縺阪ｋ縺薙→
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict[TStrategy.K(10)]=TStrategy.V(10);
            Assert.True(dict.TryGetValue(TStrategy.K(10),out var v));
            Assert.Equal(TStrategy.V(10),v);
        }
        {
            // getter 縺ｧ荳榊ｭ伜惠繧ｭ繝ｼ蜿ら・譎ゅ・萓句､・
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<KeyNotFoundException>(() => {
                var _ = dict[TStrategy.K(999)];
            });
        }
        {
            // 繝ｭ繧ｰ貅譚ｯ譎ゅ・ setter 縺ｯ萓句､・
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
            // 蟄伜惠繧ｭ繝ｼ縺ｮ蜑企勁縺ｫ謌仙粥縺吶ｋ縺薙→
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 蟄伜惠縺励↑縺・く繝ｼ縺ｯ false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Remove(TStrategy.K(99)));
        }
        {
            // Write 繝代せ縺ｮ蜑企勁縺ｫ謌仙粥縺吶ｋ縺薙→
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
            // 繧ｭ繝ｼ縺ｨ蛟､縺御ｸ閾ｴ縺吶ｋ縺ｨ蜑企勁縺輔ｌ繧・
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
            // 蛟､縺御ｸ堺ｸ閾ｴ縺ｪ繧牙炎髯､縺輔ｌ縺ｪ縺・
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.VWrong()));
            Assert.False(removed);
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 繧ｭ繝ｼ縺悟ｭ伜惠縺励↑縺代ｌ縺ｰ false
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
            // 繧ｯ繝ｪ繧｢蠕後・莉ｶ謨ｰ縺・0 縺ｫ縺ｪ繧九％縺ｨ
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
            // 霑ｽ蜉謨ｰ縺ｨ荳閾ｴ縺吶ｋ縺薙→
            Assert.Equal(2,dict.Count);
        }
    }
    [BenchFact,Benchmark]
    public void GetEnumerator() {
        {
            // IEnumerable<T> 邨檎罰縺ｧ蛻玲嫌縺ｧ縺阪ｋ縺薙→
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable<KeyValuePair<TKey,TValue>> e = dict;
            Assert.Single(e);
        }
        {
            // IEnumerable 邨檎罰縺ｧ蛻玲嫌縺ｧ縺阪ｋ縺薙→
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
    [Xunit.Fact]
    [SuppressMessage("Interoperability","CA1416:繝励Λ繝・ヨ繝輔か繝ｼ繝縺ｮ莠呈鋤諤ｧ",Justification = "<菫晉蕗荳ｭ>")]
    public void WouldOverflowMutable() {
        {
            // mutableFraction=0 縺ｧ繧よｼ邏阪〒縺阪ｋ縺薙→
            var folder = CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice,memorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,mutableFraction: 0);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TStrategy.V(1),dict[TStrategy.K(1)]);
        }
        {
            // mutable 鬆伜沺繧剃ｽｿ縺・・繧九→ OutOfMemoryException
            using var sh = TStrategy.CreateMemoryStore(memorySize: global::Seamless.Tsavorite.Const.LogMemorySize,mutableFraction: 0.5);
            using var rawDict = sh.NewDict();
            global::System.Collections.Generic.IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [BenchFact,Benchmark]
    [SuppressMessage("Interoperability","CA1416:繝励Λ繝・ヨ繝輔か繝ｼ繝縺ｮ莠呈鋤諤ｧ",Justification = "<菫晉蕗荳ｭ>")]
    public void SaveLoad() {
        var folder = CreateDiskFolder();
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
