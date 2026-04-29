using Seamless.Stores;
using System.Linq;
namespace Test.Seamless.Tsavorite;

/// <summary>
/// LightEpoch の制限を確認するテスト。
/// <list type="bullet">
///   <item><description>MaxInstances = 16: 同時に存在できる TsavoriteKV (= Store) は最大 16 個。</description></item>
///   <item><description>kTableSize = 128: 1 つの Store に同時アクセスできるスレッドは最大 128 個。</description></item>
/// </list>
/// MaxInstances 超過テストは LightEpoch のグローバル状態に依存するため、
/// <c>[Collection("LightEpochLimit")]</c> で並列実行を防いでいる。
/// </summary>
[Collection("LightEpochLimit")]
public class LightEpochLimitTests {
    /// <summary>
    /// 16 個の Store を同時に作成・保持できることを確認する (MaxInstances = 16 の正常系)。
    /// </summary>
    [Fact]
    public void MaxInstances_16Stores_AllSucceed() {
        var stores = new global::System.Collections.Generic.List<UnmanagedDictionaryStore<int,int>>();
        try {
            for(var i = 0; i<16; i++) {
                stores.Add(HashStore.Create().Create<int,int>());
            }
            Assert.Equal(16,stores.Count);
        }
        finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    /// <summary>
    /// 17 個目の Store 作成が例外になることを確認する (MaxInstances = 16 超過)。
    /// 他のテストが Store を保持した状態で並列実行されると結果が変わるため、
    /// 同コレクション内で直列実行される。
    /// </summary>
    [Fact]
    public void MaxInstances_17thStore_ThrowsException() {
        var stores = new global::System.Collections.Generic.List<UnmanagedDictionaryStore<int,int>>();
        try {
            for(var i = 0; i<16; i++) {
                stores.Add(HashStore.Create().Create<int,int>());
            }
            Assert.ThrowsAny<Exception>(() => {
                using var extra = HashStore.Create().Create<int,int>();
            });
        }
        finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    /// <summary>
    /// 128 スレッドが同一 Store に同時アクセスできることを確認する (kTableSize = 128 の正常系)。
    /// UnmanagedDictionary は Store ごとに 5 セッションを持つため、
    /// 1 Store あたり最大 floor(kTableSize / 5) = 25 インスタンスまで同時保持可能。
    /// このテストでは各スレッドが一時 dict を生成して操作し、即 Dispose する。
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

    // ── NewSession 境界値テスト ────────────────────────────────────────
    // UnmanagedDictionary は 1 インスタンスにつき 5 つの ClientSession を NewSession で確保する。
    //   Collection 基底: ReadClientSession / AddClientSession / ContainsDeleteClientSession  (3)
    //   UnmanagedDictionary 追加: Remove_TKey_TValue_ClientSession / Remove_KeyValuePair_ClientSession  (2)
    //   合計 5 セッション / dict
    //
    // kTableSize = 128 は「同時に epoch を保持しているスレッド数」の上限であり、
    // NewSession で生成できるセッション数の上限ではない。
    // セッションは操作ごとに epoch を Acquire/Release するだけなので、
    // 26 dict (130 セッション) を同時保持しても作成自体は成功する。
    // 制限が発動するのは「同時に 128 スレッドを超えるスレッドが epoch 保護区間を実行した」場合。

    /// <summary>
    /// 同一 Store から 25 個の dict を同時保持できることを確認する。
    /// </summary>
    [Fact]
    public void SessionCount_25Dicts_AllSucceed() {
        using var store = HashStore.Create().Create<int,int>();
        var dicts = new global::System.Collections.Generic.List<IDisposable>();
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
    /// kTableSize はセッション生成数を制限しないため、26 個 (130 セッション) も作成できる。
    /// </summary>
    [Fact]
    public void SessionCount_26Dicts_AllSucceed() {
        // kTableSize = 128 はセッション生成数の上限ではなく、
        // 同時 epoch 保持スレッド数の上限。26 dict (130 session) でも作成は成功する。
        using var store = HashStore.Create().Create<int,int>();
        var dicts = new global::System.Collections.Generic.List<IDisposable>();
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
    /// dict を Dispose してセッションを解放した後、再度 dict を作成できることを確認する。
    /// </summary>
    [Fact]
    public void SessionCount_DisposeAndRecreate_Succeeds() {
        using var store = HashStore.Create().Create<int,int>();
        // 25 個作成 → 全 Dispose → 再び 25 個作成できる
        for(var round = 0; round<2; round++) {
            var dicts = new global::System.Collections.Generic.List<IDisposable>();
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
/// LightEpochLimit コレクション定義。
/// このコレクションに属するテストは互いに並列実行されない。
/// </summary>
[CollectionDefinition("LightEpochLimit",DisableParallelization = true)]
public sealed class LightEpochLimitCollection;
