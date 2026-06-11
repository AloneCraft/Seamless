using Tsavorite.Stores;

using System.Linq;
namespace Test.Tsavorite.Collections.Tsavorite;

/// <summary>
/// LightEpoch 縺ｮ蛻ｶ髯舌ｒ遒ｺ隱阪☆繧九ユ繧ｹ繝医・
/// <list type="bullet">
///   <item><description>MaxInstances = 1024: 蜷梧凾縺ｫ蟄伜惠縺ｧ縺阪ｋ TsavoriteKV (= Store) 縺ｯ譛螟ｧ 1024 蛟九・/description></item>
///   <item><description>kTableSize = 128: 1 縺､縺ｮ Store 縺ｫ蜷梧凾繧｢繧ｯ繧ｻ繧ｹ縺ｧ縺阪ｋ繧ｹ繝ｬ繝・ラ縺ｯ譛螟ｧ 128 蛟九・/description></item>
/// </list>
/// MaxInstances 雜・℃繝・せ繝医・ LightEpoch 縺ｮ繧ｰ繝ｭ繝ｼ繝舌Ν迥ｶ諷九↓萓晏ｭ倥☆繧九◆繧√・
/// <c>[Collection("LightEpochLimit")]</c> 縺ｧ荳ｦ蛻怜ｮ溯｡後ｒ髦ｲ縺・〒縺・ｋ縲・
/// </summary>
[Collection("LightEpochLimit")]
public class LightEpochLimitTests {
    /// <summary>
    /// 1024 蛟九・ Store 繧貞酔譎ゅ↓菴懈・繝ｻ菫晄戟縺ｧ縺阪ｋ縺薙→繧堤｢ｺ隱阪☆繧・(MaxInstances = 1024 縺ｮ豁｣蟶ｸ邉ｻ)縲・
    /// </summary>
    [Fact]
    public void MaxInstances_1024Stores_AllSucceed() {
        var stores = new List<DictionaryStore<int,int>>();
        try {
            const int Count=1024;
            for(var i = 0; i<Count; i++) {
                stores.Add(HashStore.Create().Create<int,int>());
            }
            Assert.Equal(Count,stores.Count);
        }
        finally {
            foreach(var s in stores) s.Dispose();
        }
    }
    [Theory]
    [InlineData(16)]
    public void MaxInstances_Stores_AllSucceed(int Count) {
        var stores = new List<DictionaryStore<int,int>>();
        try {
            for(var i = 0; i<Count; i++) {
                stores.Add(HashStore.Create().Create<int,int>());
            }
            Assert.Equal(Count,stores.Count);
        }
        finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    [Fact]
    public void MaxInstances_1025Stores_ThrowsException() {
        var stores = new List<DictionaryStore<int,int>>();
        try {
            for(var i = 0; i<1024; i++) {
                stores.Add(HashStore.Create().Create<int,int>());
            }
            Assert.Throws<InvalidOperationException>(() => {
                using var extra = HashStore.Create().Create<int,int>();
            });
        }
        finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    /// <summary>
    /// 128 繧ｹ繝ｬ繝・ラ縺悟酔荳 Store 縺ｫ蜷梧凾繧｢繧ｯ繧ｻ繧ｹ縺ｧ縺阪ｋ縺薙→繧堤｢ｺ隱阪☆繧・(kTableSize = 128 縺ｮ豁｣蟶ｸ邉ｻ)縲・
    /// UnmanagedDictionary 縺ｯ Store 縺斐→縺ｫ 5 繧ｻ繝・す繝ｧ繝ｳ繧呈戟縺､縺溘ａ縲・
    /// 1 Store 縺ゅ◆繧頑怙螟ｧ floor(kTableSize / 5) = 25 繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｾ縺ｧ蜷梧凾菫晄戟蜿ｯ閭ｽ縲・
    /// 縺薙・繝・せ繝医〒縺ｯ蜷・せ繝ｬ繝・ラ縺御ｸ譎・dict 繧堤函謌舌＠縺ｦ謫堺ｽ懊＠縲∝叉 Dispose 縺吶ｋ縲・
    /// </summary>
    [Fact]
    public void kTableSize_128Threads_AllSucceed() {
        using var store = HashStore.Create().Create<int,int>();
        const int ThreadCount = 128;
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var barrier = new Barrier(ThreadCount);
        var threads = Enumerable.Range(0,ThreadCount).Select(i => new Thread(() => {
            try {
                barrier.SignalAndWait();
                using var dict = store.Create();
                dict.Add(i,i);
            }
            catch(Exception ex) {
                errors.Add(ex);
            }
        })).ToArray();
        foreach(var t in threads) t.Start();
        foreach(var t in threads) t.Join();
        Assert.Empty(errors);
    }

    // 笏笏 NewSession 蠅・阜蛟､繝・せ繝・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // UnmanagedDictionary 縺ｯ 1 繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ縺ｫ縺､縺・5 縺､縺ｮ ClientSession 繧・NewSession 縺ｧ遒ｺ菫昴☆繧九・
    //   Collection 蝓ｺ蠎・ ReadClientSession / AddClientSession / ContainsDeleteClientSession  (3)
    //   UnmanagedDictionary 霑ｽ蜉: Remove_TKey_TValue_ClientSession / Remove_KeyValuePair_ClientSession  (2)
    //   蜷郁ｨ・5 繧ｻ繝・す繝ｧ繝ｳ / dict
    //
    // kTableSize = 128 縺ｯ縲悟酔譎ゅ↓ epoch 繧剃ｿ晄戟縺励※縺・ｋ繧ｹ繝ｬ繝・ラ謨ｰ縲阪・荳企剞縺ｧ縺ゅｊ縲・
    // NewSession 縺ｧ逕滓・縺ｧ縺阪ｋ繧ｻ繝・す繝ｧ繝ｳ謨ｰ縺ｮ荳企剞縺ｧ縺ｯ縺ｪ縺・・
    // 繧ｻ繝・す繝ｧ繝ｳ縺ｯ謫堺ｽ懊＃縺ｨ縺ｫ epoch 繧・Acquire/Release 縺吶ｋ縺縺代↑縺ｮ縺ｧ縲・
    // 26 dict (130 繧ｻ繝・す繝ｧ繝ｳ) 繧貞酔譎ゆｿ晄戟縺励※繧ゆｽ懈・閾ｪ菴薙・謌仙粥縺吶ｋ縲・
    // 蛻ｶ髯舌′逋ｺ蜍輔☆繧九・縺ｯ縲悟酔譎ゅ↓ 128 繧ｹ繝ｬ繝・ラ繧定ｶ・∴繧九せ繝ｬ繝・ラ縺・epoch 菫晁ｭｷ蛹ｺ髢薙ｒ螳溯｡後＠縺溘榊ｴ蜷医・

    /// <summary>
    /// 蜷御ｸ Store 縺九ｉ 25 蛟九・ dict 繧貞酔譎ゆｿ晄戟縺ｧ縺阪ｋ縺薙→繧堤｢ｺ隱阪☆繧九・
    /// </summary>
    [Fact]
    public void SessionCount_25Dicts_AllSucceed() {
        using var store = HashStore.Create().Create<int,int>();
        var dicts = new List<IDisposable>();
        try {
            for(var i = 0; i<25; i++) {
                dicts.Add(store.Create());
            }
            Assert.Equal(25,dicts.Count);
        }
        finally {
            foreach(var d in dicts) d.Dispose();
        }
    }

    /// <summary>
    /// kTableSize 縺ｯ繧ｻ繝・す繝ｧ繝ｳ逕滓・謨ｰ繧貞宛髯舌＠縺ｪ縺・◆繧√・6 蛟・(130 繧ｻ繝・す繝ｧ繝ｳ) 繧ゆｽ懈・縺ｧ縺阪ｋ縲・
    /// </summary>
    [Fact]
    public void SessionCount_26Dicts_AllSucceed() {
        // kTableSize = 128 縺ｯ繧ｻ繝・す繝ｧ繝ｳ逕滓・謨ｰ縺ｮ荳企剞縺ｧ縺ｯ縺ｪ縺上・
        // 蜷梧凾 epoch 菫晄戟繧ｹ繝ｬ繝・ラ謨ｰ縺ｮ荳企剞縲・6 dict (130 session) 縺ｧ繧ゆｽ懈・縺ｯ謌仙粥縺吶ｋ縲・
        using var store = HashStore.Create().Create<int,int>();
        var dicts = new List<IDisposable>();
        try {
            for(var i = 0; i<26; i++) {
                dicts.Add(store.Create());
            }
            Assert.Equal(26,dicts.Count);
        }
        finally {
            foreach(var d in dicts) d.Dispose();
        }
    }

    /// <summary>
    /// dict 繧・Dispose 縺励※繧ｻ繝・す繝ｧ繝ｳ繧定ｧ｣謾ｾ縺励◆蠕後∝・蠎ｦ dict 繧剃ｽ懈・縺ｧ縺阪ｋ縺薙→繧堤｢ｺ隱阪☆繧九・
    /// </summary>
    [Fact]
    public void SessionCount_DisposeAndRecreate_Succeeds() {
        using var store = HashStore.Create().Create<int,int>();
        // 25 蛟倶ｽ懈・ 竊・蜈ｨ Dispose 竊・蜀阪・ 25 蛟倶ｽ懈・縺ｧ縺阪ｋ
        for(var round = 0; round<2; round++) {
            var dicts = new List<IDisposable>();
            try {
                for(var i = 0; i<25; i++) {
                    dicts.Add(store.Create());
                }
                Assert.Equal(25,dicts.Count);
            }
            finally {
                foreach(var d in dicts) d.Dispose();
            }
        }
    }
}

/// <summary>
/// LightEpochLimit 繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ螳夂ｾｩ縲・
/// 縺薙・繧ｳ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ縺ｫ螻槭☆繧九ユ繧ｹ繝医・莠偵＞縺ｫ荳ｦ蛻怜ｮ溯｡後＆繧後↑縺・・
/// </summary>
[CollectionDefinition("LightEpochLimit",DisableParallelization = true)]
public sealed class LightEpochLimitCollection;
