using Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Tsavorite.Tsavorite;

/// <summary>
/// Tsavorite の LightEpoch MaxInstances = 1024 の制限をテストする。
/// TsavoriteKV インスタンスを作成するたびに LightEpoch インスタンスが 1 つ確保される。
/// MaxInstances=1024 の静的上限により、1025 個同時作成はエラーになる。
/// </summary>
public class LightEpochInstanceLimitTests(ITestOutputHelper output) {
    private static string TempDir(string name) =>
        TestPath.Directory<LightEpochInstanceLimitTests>(name);

    [Fact]
    public void Can_Create_16_Stores() {
        var stores = new List<HashSetStore<int>>();
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
    [InlineData(1025)]
    public void Cannot_Create_Stores_Simultaneously(int Count) {
        var stores = new List<HashSetStore<int>>();

        try {
            for(int i = 0; i<1024; i++) {
                var dir = TempDir($"store-{i}");
                var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                stores.Add(store);
            }
            var failureAt = -1;
            for(int i = 1024; i<Count; i++) {
                try {
                    var dir = TempDir($"store-{i}");
                    var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                        Devices.CreateLogDevice(Path.Combine(dir,"log")),
                        Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                        new BlittableStrategy()
                    );
                    stores.Add(store);
                }
                catch(InvalidOperationException ex) when(ex.Message.Contains("Exceeded maximum number of active LightEpoch instances",StringComparison.Ordinal)) {
                    failureAt=i;
                    output.WriteLine($"✓ Store creation failed at iteration {i} with expected error: {ex.Message}");
                    break;
                }
            }
            Assert.Equal(1024,stores.Count);
            Assert.True(failureAt>=1024,$"Expected store creation to fail after 1024 active stores, but it did not fail for Count={Count}.");
        }
        finally {
            foreach(var store in stores) {
                store?.Dispose();
            }
        }
    }

    [Fact]
    public void Can_Reuse_Slots_After_Dispose() {
        var stores = new List<HashSetStore<int>>();

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
        // スロットが 1024 に満たないうちに新規作成はできるが、1024 を超えるとエラー
        const int Half = 512;
        var slots = new Stack<HashSetStore<int>>();

        try {
            // Half 個作成
            for(int i = 0; i<Half; i++) {
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
            output.WriteLine($"Step 1: Created {Half} stores ({Half}/1024 slots used)");

            // さらに Half 個作成 → 合計 1024 個
            for(int i = 0; i<Half; i++) {
                var dir = TempDir($"interleaved-{Half+i}");
                var store = HashStore.Create(CheckpointDir: Path.Combine(dir,"checkpoint")).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dir,"log")),
                    Devices.CreateLogDevice(Path.Combine(dir,"obj")),
                    new BlittableStrategy()
                );
                slots.Push(store);
            }
            output.WriteLine($"Step 2: Created {Half} more stores (1024/1024 slots used)");

            // 1 個 Dispose してスロット開放
            var released = slots.Pop();
            released.Dispose();
            output.WriteLine($"Step 3: Disposed 1 store (1023/1024 slots used)");

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
            output.WriteLine($"Step 4: ✓ Successfully created 1 more store in released slot (1024/1024 slots used again)");

            // もう 1 個作成しようとするとエラー
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var dirOverflow = TempDir($"interleaved-overflow");
                HashStore.Create(
                    CheckpointDir: Path.Combine(dirOverflow,"checkpoint")
                ).Create<int>(
                    Devices.CreateLogDevice(Path.Combine(dirOverflow,"log")),
                    Devices.CreateLogDevice(Path.Combine(dirOverflow,"obj")),
                    new BlittableStrategy()
                );
            });
            output.WriteLine($"Step 5: ✓ Attempt to create 1025th store correctly failed: {ex.Message}");
        }
        finally {
            while(slots.Count>0) {
                slots.Pop().Dispose();
            }
        }
    }
}
