using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Tsavorite;

/// <summary>
/// Tsavorite の LightEpoch MaxInstances = 16 の制限をテストする。
/// TsavoriteKV インスタンスを作成するたびに LightEpoch インスタンスが 1 つ確保される。
/// MaxInstances=16 の静的上限により、17 個同時作成はエラーになる。
/// </summary>
public class LightEpochInstanceLimitTests(ITestOutputHelper output) {
    private static string TempDir(string name) =>
        global::Test.Seamless.TestPath.Directory<LightEpochInstanceLimitTests>(name);

    [Fact]
    public void Can_Create_16_Stores() {
        var stores = new global::System.Collections.Generic.List<HashSetStore<int>>();
        try {
            for(int i = 0; i<16; i++) {
                var dir = TempDir($"store-{i}");
                var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                stores.Add(store);
            }
            Assert.Equal(16,stores.Count);
            output.WriteLine($"✓ Successfully created 16 stores (each uses 1 LightEpoch instance)");
        }
        finally {
            foreach(var store in stores) {
                store?.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(17)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    public void Cannot_Create_Stores_Simultaneously(int Count) {
        var stores = new List<HashSetStore<int>>();

        try {
            for(int i = 0; i<Count; i++) {
                var dir = TempDir($"store-{i}");
                var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                stores.Add(store);
                // try {
                //     var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                //         Devices.CreateLogDevice(Path.Combine(dir,"log")),
                //         Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                //         new BlittableStrategy()
                //     );
                //     stores.Add(store);
                // }
                // catch(InvalidOperationException ex) when(ex.Message.Contains("Exceeded maximum number of active LightEpoch instances")) {
                //     output.WriteLine($"✓ 17th store creation failed at iteration {i} with expected error:");
                //     output.WriteLine($"  {ex.Message}");

                //     // 17個目でエラーが発生したことを確認
                //     Assert.Equal(16,stores.Count);
                //     return;// テスト成功
                // }
            }

            // 17個すべて作成できたら失敗（元々限界に達しているはず）
            Assert.Fail("Expected InvalidOperationException when creating 17th store, but all 17 were created");
        }
        finally {
            foreach(var store in stores) {
                store?.Dispose();
            }
        }
    }

    [Fact]
    public void Can_Reuse_Slots_After_Dispose() {
        var stores = new global::System.Collections.Generic.List<HashSetStore<int>>();

        try {
            // バッチ 1: 16 ストアを作成
            for(int i = 0; i<16; i++) {
                var dir = TempDir($"batch1-store-{i}");
                var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
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
                var dir = TempDir($"batch2-store-{i}");
                var store = HashStore.Create(
                    CheckpointDir: Path.Combine(dir,"checkpoint")
                ).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                stores.Add(store);
            }
            Assert.Equal(16,stores.Count);
            output.WriteLine($"[Batch 2] ✓ Successfully reused slots to create 16 more stores");
        }
        finally {
            foreach(var store in stores) {
                store?.Dispose();
            }
        }
    }

    [Fact]
    public void Interleaved_Create_And_Dispose_Respects_Limit() {
        // スロットが 16 に満たないうちに新規作成はできるが、16 を超えるとエラー
        var slots = new Stack<HashSetStore<int>>();

        try {
            // 8 個作成
            for(int i = 0; i<8; i++) {
                var dir = TempDir($"interleaved-{i}");
                var store = HashStore.Create(
                    CheckpointDir: Path.Combine(dir,"checkpoint")
                ).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                slots.Push(store);
            }
            output.WriteLine($"Step 1: Created 8 stores (8/16 slots used)");

            // さらに 8 個作成 → 合計 16 個
            for(int i = 0; i<8; i++) {
                var dir = TempDir($"interleaved-{8+i}");
                var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                slots.Push(store);
            }
            output.WriteLine($"Step 2: Created 8 more stores (16/16 slots used)");

            // 1 個 Dispose してスロット開放
            var released = slots.Pop();
            released.Dispose();
            output.WriteLine($"Step 3: Disposed 1 store (15/16 slots used)");

            // 1 個作成 → スロット復帰
            var dir9 = TempDir($"interleaved-reuse");
            var store9 = HashStore.Create(
                CheckpointDir: Path.Combine(dir9,"checkpoint")
            ).Create<int>(
                Devices.CreateLogDevice(Path.Combine(dir9,"log")),
                Devices.CreateLogDevice(Path.Combine(dir9,"obj")),
                new BlittableStrategy()
            );
            slots.Push(store9);
            output.WriteLine($"Step 4: ✓ Successfully created 1 more store in released slot (16/16 slots used again)");

            // もう 1 個作成しようとするとエラー
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var dir17 = TempDir($"interleaved-overflow");
                HashStore.Create(
                    CheckpointDir: Path.Combine(dir17,"checkpoint")
                ).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir17,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir17,"obj")),
                    new BlittableStrategy()
                );
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
