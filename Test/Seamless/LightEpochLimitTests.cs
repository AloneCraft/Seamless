using Seamless.Tsavorite.Stores;
namespace Test.Seamless;

/// <summary>
/// LightEpoch の制限を確認するテスト、E
/// <list type="bullet">
///   <item><description>MaxInstances = 1024: 同時に存在できる TsavoriteKV (= Store) は最大 1024 個、E/description></item>
/// LightEpoch の取得と解放を検証する。
/// </list>
/// LightEpoch の取得と解放を検証する。
/// LightEpoch の取得と解放を検証する。
/// </summary>
[Collection("LightEpochLimit")]
public class LightEpochLimitTests {
    /// <summary>
    /// LightEpoch のインスタンスプールはプロセス全体で共有される静的 1024 スロットである。
    /// 同一プロセスで先に走った他テストが残したファイナライザ待ちのインスタンスを回収し、
    /// プールを空にしてから上限ちょうどの検証を行えるようにする。
    /// </summary>
    private static void ReclaimEpochPool() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// LightEpoch の取得と解放を検証する。
    /// ちょうど 1024 個まで作成でき、1025 個目で
    /// "Exceeded maximum number of active LightEpoch instances" が投げられる。
    /// </summary>
    [Fact]
    public void MaxInstances_Exactly1024_ThenThrows() {
        ReclaimEpochPool();
        var stores = new List<DictionaryStore<int,int>>();
        try {
            InvalidOperationException? overflow = null;
            for(var i = 0;i<1025;i++) {
                try {
                    stores.Add(Store.Create().Create<int,int>());
                } catch(InvalidOperationException ex) when(ex.Message.Contains("Exceeded maximum number of active LightEpoch instances",StringComparison.Ordinal)) {
                    overflow=ex;
                    break;
                }
            }
            Assert.NotNull(overflow);          // 上限が機能している
            Assert.Equal(1024,stores.Count);   // ちょうど 1024 成功
        } finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    /// <summary>
    /// MaxInstances 未満であれば普通に複数 Store を同時保持できる (正常系)。
    /// </summary>
    [Theory]
    [InlineData(16)]
    public void MaxInstances_Stores_AllSucceed(int Count) {
        ReclaimEpochPool();
        var stores = new List<DictionaryStore<int,int>>();
        try {
            for(var i = 0;i<Count;i++) {
                stores.Add(Store.Create().Create<int,int>());
            }
            Assert.Equal(Count,stores.Count);
        } finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    /// <summary>
    /// Dispose でスロットが解放され、再利用できることを確認する。
    /// 16 作成 → 全 Dispose → 再度 16 作成が成功する。
    /// </summary>
    [Fact]
    public void Can_Reuse_Slots_After_Dispose() {
        ReclaimEpochPool();
        var stores = new List<DictionaryStore<int,int>>();
        try {
            for(var i = 0;i<16;i++) {
                stores.Add(Store.Create().Create<int,int>());
            }
            foreach(var s in stores) {
                s.Dispose();
            }
            stores.Clear();

            for(var i = 0;i<16;i++) {
                stores.Add(Store.Create().Create<int,int>());
            }
            Assert.Equal(16,stores.Count);
        } finally {
            foreach(var s in stores) s.Dispose();
        }
    }

    /// <summary>
    /// スロットの解放と確保が境界 (1024) で正しく振る舞うことを確認する。
    /// 1024 充填 → 1 Dispose → 1 作成成功 (1024/1024) → 1025 個目で throw。
    /// </summary>
    [Fact]
    public void Interleaved_Create_And_Dispose_Respects_Limit() {
        ReclaimEpochPool();
        var slots = new Stack<DictionaryStore<int,int>>();
        try {
            for(var i = 0;i<1024;i++) {
                slots.Push(Store.Create().Create<int,int>());
            }

            // 1 個 Dispose してスロットを解放
            slots.Pop().Dispose();

            // 解放したスロットへ 1 個作成 → 成功 (1024/1024)
            slots.Push(Store.Create().Create<int,int>());
            Assert.Equal(1024,slots.Count);

            // もう 1 個作成しようとすると上限超過で throw
            Assert.Throws<InvalidOperationException>(() => {
                using var overflow = Store.Create().Create<int,int>();
            });
        } finally {
            while(slots.Count>0) {
                slots.Pop().Dispose();
            }
        }
    }

    /// <summary>
    /// LightEpoch の取得と解放を検証する。
    /// LightEpoch の取得と解放を検証する。
    /// 1 Store あたり最大 floor(kTableSize / 5) = 25 インスタンスまで同時保持可能、E
    /// LightEpoch の取得と解放を検証する。
    /// </summary>
    [Fact]
    public void kTableSize_128Threads_AllSucceed() {
        using var store = Store.Create().Create<int,int>();
        const int ThreadCount = 128;
        var errors = new global::System.Collections.Concurrent.ConcurrentBag<Exception>();
        var barrier = new Barrier(ThreadCount);
        var threads = Enumerable.Range(0,ThreadCount).Select(i => new Thread(() => {
            try {
                barrier.SignalAndWait();
                using var dict = store.Create();
                dict.Add(i,i);
            } catch(Exception ex) {
                errors.Add(ex);
            }
        })).ToArray();
        foreach(var t in threads) t.Start();
        foreach(var t in threads) t.Join();
        Assert.Empty(errors);
    }

    // NewSession 境界値テスト
    // UnmanagedDictionary は 1 インスタンスにつき 5 つの ClientSession を NewSession で確保する。
    //   Collection 基底: ReadClientSession / AddClientSession / ContainsDeleteClientSession  (3)
    //   UnmanagedDictionary 追加: Remove_TKey_TValue_ClientSession / Remove_KeyValuePair_ClientSession  (2)
    // dict 作成数を増やして session 上限への到達を確認する。
    //

    /// <summary>
    /// </summary>
    [Fact]
    public void SessionCount_25Dicts_AllSucceed() {
        using var store = Store.Create().Create<int,int>();
        var dicts = new List<IDisposable>();
        try {
            for(var i = 0;i<25;i++) {
                dicts.Add(store.Create());
            }
            Assert.Equal(25,dicts.Count);
        } finally {
            foreach(var d in dicts) d.Dispose();
        }
    }

    /// <summary>
    /// LightEpoch の取得と解放を検証する。
    /// </summary>
    [Fact]
    public void SessionCount_26Dicts_AllSucceed() {
        // LightEpoch の取得と解放を検証する。
        // LightEpoch の取得と解放を検証する。
        using var store = Store.Create().Create<int,int>();
        var dicts = new List<IDisposable>();
        try {
            for(var i = 0;i<26;i++) {
                dicts.Add(store.Create());
            }
            Assert.Equal(26,dicts.Count);
        } finally {
            foreach(var d in dicts) d.Dispose();
        }
    }

    /// <summary>
    /// LightEpoch の取得と解放を検証する。
    /// </summary>
    [Fact]
    public void SessionCount_DisposeAndRecreate_Succeeds() {
        using var store = Store.Create().Create<int,int>();
        // LightEpoch の取得と解放を検証する。
        for(var round = 0;round<2;round++) {
            var dicts = new List<IDisposable>();
            try {
                for(var i = 0;i<25;i++) {
                    dicts.Add(store.Create());
                }
                Assert.Equal(25,dicts.Count);
            } finally {
                foreach(var d in dicts) d.Dispose();
            }
        }
    }
}

/// <summary>
/// LightEpochLimit コレクション定義、E
/// LightEpoch の取得と解放を検証する。
/// </summary>
[CollectionDefinition("LightEpochLimit",DisableParallelization = true)]
public sealed class LightEpochLimitCollection;
