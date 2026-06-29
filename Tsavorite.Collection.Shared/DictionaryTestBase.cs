using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Collections;
using Tsavorite.core;
namespace Tsavorite.Collection;
/// <summary>
/// <see cref="global::Seamless.Tsavorite.Collections.IDictionary{TKey,TValue}"/> 蝗ｺ譛峨・繝・せ繝亥渕蠎輔け繝ｩ繧ｹ縲・/// <see cref="global::System.Collections.Generic.IDictionary{TKey,TValue}"/> 蜈ｱ騾壹ユ繧ｹ繝医・ <see cref="IDictionaryTestBase{TStrategy,TKey,TValue}"/> 縺ｫ髮・ｴ・＆繧後※縺翫ｊ縲・/// 縺薙・繧ｯ繝ｩ繧ｹ縺ｧ縺ｯ霑ｽ蜉縺ｧ TryAdd縲ヽemove(out value)縲．ispose縲・ntryCount 繧堤｢ｺ隱阪☆繧九・/// </summary>
public abstract class DictionaryTestBase<TStrategy, TKey, TValue>:IDictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private string CreateDiskFolder() =>
        TestPath.Directory(this);
    [BenchFact,Benchmark]
    public void TryAdd() {
        {
            // 譁ｰ隕上く繝ｼ縺ｮ霑ｽ蜉縺ｯ Success
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TryAddResult.Success,result);
        }
        {
            // 驥崎､・く繝ｼ縺ｮ霑ｽ蜉縺ｯ KeyAlreadyExists
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(2));
            Assert.Equal(TryAddResult.KeyAlreadyExists,result);
        }
        {
            // 繧ｳ繝ｼ繝ｫ繝峨ョ繝ｼ繧ｿ隱ｭ縺ｿ霎ｼ縺ｿ蠕後・驥崎､・ｿｽ蜉縺ｧ繧・KeyAlreadyExists
            var folder = this.CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(TStrategy.K(0),TStrategy.V(1)));
        }
    }
    [BenchFact,Benchmark]
    public void Remove_key_value() {
        {
            // 蟄伜惠縺吶ｋ繧ｭ繝ｼ縺ｯ蜑企勁蛟､繧定ｿ斐☆
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(TStrategy.K(1),out var value);
            Assert.True(removed);
            Assert.Equal(TStrategy.V(1),value);
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // 蟄伜惠縺励↑縺・く繝ｼ縺ｯ false 縺ｨ default 繧定ｿ斐☆
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var removed = dict.Remove(TStrategy.K(99),out var value);
            Assert.False(removed);
            Assert.Equal(default,value);
        }
    }
    [BenchFact,Benchmark]
    public void Dispose() {
        var sh = TStrategy.CreateMemoryStore();
        var dict = sh.NewDict();
        {
            // Dispose 縺ｯ隍・焚蝗槫他繧薙〒繧ょｮ牙・縺ｪ縺薙→
            Assert.False(dict.IsDisposed);
            dict.Dispose();
            Assert.True(dict.IsDisposed);
            dict.Dispose();
            sh.Dispose();
        }
    }
    [BenchFact,Benchmark]
    public void EntryCount() {
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        {
            // 繧ｹ繝医い荳翫・莉ｶ謨ｰ縺・1 莉･荳翫〒縺ゅｋ縺薙→
            Assert.True(dict.EntryCount>=1);
        }
    }
}
