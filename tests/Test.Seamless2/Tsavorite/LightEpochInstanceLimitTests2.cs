using Seamless;
using Tsavorite.core;

namespace Test.Seamless.TsavoriteLimits;

/// <summary>
/// Tsavorite 2.0.0-beta3 の LightEpoch MaxInstances = 16 の制限をテストする。
/// TsavoriteKV インスタンスを作成するたびに LightEpoch インスタンスが 1 つ確保される。
/// MaxInstances=16 の静的上限により、17 個同時作成はエラーになる。
/// </summary>
[Collection("TsavoriteTests")]
public class LightEpochInstanceLimitTests2(ITestOutputHelper output) {
    private static string TempDir(string name) =>
        Path.Combine(Path.GetTempPath(),"LightEpochTest",Guid.NewGuid().ToString("N"),name);

    private sealed class NativeStoreHandle:IDisposable {
        private readonly string rootDir;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly KVSettings settings;
        private readonly TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store;

        public NativeStoreHandle(string rootDir) {
            this.rootDir=rootDir;
            this.logDevice=Devices.CreateLogDevice(Path.Combine(rootDir,"log"));
            this.objDevice=Devices.CreateLogDevice(Path.Combine(rootDir,"obj"));
            this.settings=new KVSettings {
                IndexSize=64,
                MutableFraction=0.1,
                LogMemorySize=1024*1024,
                PageSize=1L<<12,
                SegmentSize=1L<<20,
                LogDevice=this.logDevice,
                ObjectLogDevice=this.objDevice,
                ObjectLogSegmentSize=Const.MinObjectLogCapacity,
                CheckpointDir=Path.Combine(rootDir,"checkpoint")
            };

            var storeFunctions = new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(
                new SpanByteComparer(),
                static () => null,
                new DefaultRecordDisposer()
            );

            this.Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
                this.settings,
                storeFunctions,
                static (allocSettings,funcs) => new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(allocSettings,funcs)
            );
        }

        public void Dispose() {
            this.Store.Log.FlushAndEvict(wait: true);
            this.Store.Dispose();
            this.settings.Dispose();
            this.logDevice.Dispose();
            this.objDevice.Dispose();
            try { Directory.Delete(this.rootDir,true); } catch { }
        }
    }

    private static NativeStoreHandle CreateNativeStore(string name) => new(TempDir(name));

    [Fact]
    public void Can_Create_16_Stores() {
        var stores = new List<NativeStoreHandle>();
        try {
            for(int i = 0; i<16; i++) {
                var store = CreateNativeStore($"store-{i}");
                stores.Add(store);
            }
            Assert.Equal(16,stores.Count);
            output.WriteLine($"✓ Successfully created 16 stores (each uses 1 LightEpoch instance)");
        }
        finally {
            foreach(var store in stores) {
                store.Dispose();
            }
        }
    }

    [Fact]
    public void Cannot_Create_17_Stores_Simultaneously() {
        var stores = new List<NativeStoreHandle>();

        try {
            for(int i = 0; i<17; i++) {
                try {
                    var store = CreateNativeStore($"store-{i}");
                    stores.Add(store);
                }
                catch(InvalidOperationException ex) when(ex.Message.Contains("Exceeded maximum number of active LightEpoch instances")) {
                    output.WriteLine($"✓ 17th store creation failed at iteration {i} with expected error:");
                    output.WriteLine($"  {ex.Message}");

                    // 17個目でエラーが発生したことを確認
                    Assert.Equal(16,stores.Count);
                    return; // テスト成功
                }
            }

            // 17個すべて作成できたら失敗（元々限界に達しているはず）
            Assert.Fail("Expected InvalidOperationException when creating 17th store, but all 17 were created");
        }
        finally {
            foreach(var store in stores) {
                store.Dispose();
            }
        }
    }

    [Fact]
    public void Can_Reuse_Slots_After_Dispose() {
        var stores = new List<NativeStoreHandle>();

        try {
            // バッチ 1: 16 ストアを作成
            for(int i = 0; i<16; i++) {
                var store = CreateNativeStore($"batch1-store-{i}");
                stores.Add(store);
            }
            output.WriteLine($"[Batch 1] Created {stores.Count} stores (all 16 slots used)");

            // バッチ 1 を Dispose してスロットを解放
            foreach(var store in stores) {
                store.Dispose();
            }
            stores.Clear();
            output.WriteLine($"[Batch 1] Disposed all stores (slots now available for reuse)");

            // バッチ 2: 再度 16 ストアを作成 → スロットが解放されたので成功するはず
            for(int i = 0; i<16; i++) {
                var store = CreateNativeStore($"batch2-store-{i}");
                stores.Add(store);
            }
            Assert.Equal(16,stores.Count);
            output.WriteLine($"[Batch 2] ✓ Successfully reused slots to create 16 more stores");
        }
        finally {
            foreach(var store in stores) {
                store.Dispose();
            }
        }
    }

    [Fact]
    public void Interleaved_Create_And_Dispose_Respects_Limit() {
        // スロットが 16 に満たないうちに新規作成はできるが、16 を超えるとエラー
        var slots = new Stack<NativeStoreHandle>();

        try {
            // 8 個作成
            for(int i = 0; i<8; i++) {
                var store = CreateNativeStore($"interleaved-{i}");
                slots.Push(store);
            }
            output.WriteLine($"Step 1: Created 8 stores (8/16 slots used)");

            // さらに 8 個作成 → 合計 16 個
            for(int i = 0; i<8; i++) {
                var store = CreateNativeStore($"interleaved-{8+i}");
                slots.Push(store);
            }
            output.WriteLine($"Step 2: Created 8 more stores (16/16 slots used)");

            // 1 個 Dispose してスロット開放
            var released = slots.Pop();
            released.Dispose();
            output.WriteLine($"Step 3: Disposed 1 store (15/16 slots used)");

            // 1 個作成 → スロット復帰
            var store9 = CreateNativeStore("interleaved-reuse");
            slots.Push(store9);
            output.WriteLine($"Step 4: ✓ Successfully created 1 more store in released slot (16/16 slots used again)");

            // もう 1 個作成しようとするとエラー
            var ex = Assert.Throws<InvalidOperationException>(() => {
                using var overflow = CreateNativeStore("interleaved-overflow");
            });
            output.WriteLine($"Step 5: ✓ Attempt to create 17th store correctly failed: {ex.Message}");
        }
        finally {
            while(slots.Count>0) {
                slots.Pop().Dispose();
            }
        }
    }
}
