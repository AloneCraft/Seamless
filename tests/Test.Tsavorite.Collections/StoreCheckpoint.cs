using Tsavorite.Stores;
using System.Linq;
using System.Runtime.Versioning;
using Tsavorite.core;
namespace Test.Tsavorite.Collections;

public class StoreCheckpoint {
    //private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,"TsavoriteTest_"+Guid.NewGuid().ToString("N"));
    private static string CreateFolder() => TestPath.Directory<StoreCheckpoint>();
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        var ct = TestContext.Current.CancellationToken;
        using var store = HashStore.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveAsync(ct: ct));
    }
    [Fact, Trait("Category","Unit")]
    public async Task LoadAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        var ct = TestContext.Current.CancellationToken;
        using var store = HashStore.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.LoadAsync(ct));
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        var ct = TestContext.Current.CancellationToken;
        using var store = HashStore.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveLogOnlyAsync(ct: ct));
    }
    [Fact, Trait("Category","Unit")]
    public void Save_Load_HashSet_データが永続化され復元される() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            using var set = store.Create();
            set[1]=11;
            set[2]=22;
            set[3]=33;
            Assert.True(set.Contains(1));
            token=store.Save();
            Assert.True(set.Contains(1));
        }
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            store.Load(token);
            var (begin2,head2,ro2,tail2)=store.Address;
            using var set = store.Create();
            var l = set.ToArray();
            var r = set.Contains(1);
            Assert.True(set.Contains(1),$"Contains(1) failed. EntryCount={set.EntryCount}, Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_LoadAsync_HashSet_データが永続化され復元される() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            token=await store.SaveAsync(ct: ct);
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            var (begin2,head2,ro2,tail2)=store.Address;
            using var set = store.Create();
            Assert.True(set.Contains(1),$"Contains(1) failed. EntryCount={set.EntryCount}, Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_LoadAsync_Dictionary_データが永続化され復元される() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var dict = store.Create();
            Assert.True(dict.ContainsKey(1));
            Assert.Equal("one",dict[1]);
            Assert.True(dict.ContainsKey(2));
            Assert.Equal("two",dict[2]);
            Assert.False(dict.ContainsKey(99));
        }
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_LoadAsync_HashSet_ハッシュインデックスが復元されContainsが動作する() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            for(var i = 0; i<100; i++) set.Add(i);
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            for(var i = 0; i<100; i++) Assert.True(set.Contains(i),$"Contains({i}) failed after recovery");
            Assert.False(set.Contains(999));
        }
    }
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_LoadAsync_Dictionary_ハッシュインデックスが復元されReadが動作する() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            for(var i = 0; i<50; i++) dict.Add(i,$"val_{i}");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var dict = store.Create();
            for(var i = 0; i<50; i++) {
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
            Assert.False(dict.ContainsKey(999));
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Save_Load_Dictionary_Add件数に応じてWithRecoveryIndexSizeがIndexSizeを拡張する() {
        const long initialIndexSize = 64;
        const int entryCount = 100;
        var expectedIndexSize = HashStore.GetIndexSizeForCapacity(entryCount,initialIndexSize);
        Assert.True(expectedIndexSize>initialIndexSize);

        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            var factory = HashStore.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
            using var store = factory.Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();

            for(var i = 0; i<entryCount; i++)
                dict.Add(i,$"val_{i}");

            token=store.Save();
        }

        var recoveryFactory = HashStore.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
        Assert.Equal(expectedIndexSize,recoveryFactory.ResolveRecoveryIndexSize(token,initialIndexSize));

        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = recoveryFactory.WithRecoveryIndexSize(token,initialIndexSize).Create<int,string>(logDevice,objDevice);
            store.Load(token);
            using var dict = store.Create();

            Assert.Equal(entryCount,dict.GetLongCount());
            for(var i = 0; i<entryCount; i++) {
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after resize-aware recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void SaveLogOnly_LoadLatest_Dictionary_Add件数に応じてWithRecoveryIndexSizeがIndexSizeを拡張する() {
        const long initialIndexSize = 64;
        const int entryCount = 100;
        var expectedIndexSize = HashStore.GetIndexSizeForCapacity(entryCount,initialIndexSize);
        Assert.True(expectedIndexSize>initialIndexSize);

        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            var factory = HashStore.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
            using var store = factory.Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();

            for(var i = 0; i<entryCount; i++)
                dict.Add(i,$"val_{i}");

            _=store.SaveLogOnly();
        }

        var recoveryFactory = HashStore.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
        Assert.Equal(expectedIndexSize,recoveryFactory.ResolveRecoveryIndexSize(minimumIndexSize: initialIndexSize));

        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = recoveryFactory.WithRecoveryIndexSize(minimumIndexSize: initialIndexSize).Create<int,string>(logDevice,objDevice);
            store.Load();
            using var dict = store.Create();

            Assert.Equal(entryCount,dict.GetLongCount());
            for(var i = 0; i<entryCount; i++) {
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after latest resize-aware recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_CheckpointFailed_InvalidOperationExceptionをスロー() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = Devices.CreateLogDevice(folder);
        using var objDevice = Devices.CreateLogDevice(folder+"_obj");
        using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        // SaveAsync が同期的にロックを取得してから非同期 I/O を待機するため、
        // await せずタスクを保持しておくことでロックが取得された状態を維持できる
        var ongoingCheckpoint = store.SaveAsync(ct: ct).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(ct: ct).AsTask());
        await ongoingCheckpoint;
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_CheckpointFailed_InvalidOperationExceptionをスロー() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = Devices.CreateLogDevice(folder);
        using var objDevice = Devices.CreateLogDevice(folder+"_obj");
        using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        // SaveLogOnlyAsync が同期的にロックを取得してから非同期 I/O を待機するため、
        // await せずタスクを保持しておくことでロックが取得された状態を維持できる
        var ongoingCheckpoint = store.SaveLogOnlyAsync(ct: ct).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveLogOnlyAsync(ct: ct).AsTask());
        await ongoingCheckpoint;
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_CompleteCheckpointAsyncキャンセル_OperationCanceledExceptionをスロー() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = Devices.CreateLogDevice(folder);
        using var objDevice = Devices.CreateLogDevice(folder+"_obj");
        using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(ct: cts.Token).AsTask());
    }
    [SupportedOSPlatform("windows")]
    private class SimulatedFlakyDevice(string path):LocalStorageDevice(path,false,false,false,-1,false,false,false) {
        // 失敗させるかどうかのスイッチ
        public bool ShouldFail { get; set; } = false;
        // チェックポイント進行中状態を作るための遅延(ms)
        public int WriteDelayMilliseconds { get; set; } = 0;

        public override void WriteAsync(IntPtr srcAddress,int alignedSourceNextAddress,ulong alignedDestinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object callbackContext) {
            if(this.ShouldFail) {
                // 第1引数に 0 以外（エラーコード）を渡してコールバックを即時実行
                // これにより Tsavorite 内部でチェックポイント失敗のフローが走る
                const uint ERROR_CODE = 0xFFFFFFFF;
                callback(ERROR_CODE,numBytesToWrite,callbackContext);
                return;
            }

            if(this.WriteDelayMilliseconds>0)
                Thread.Sleep(this.WriteDelayMilliseconds);

            // 通常の書き込み処理
            base.WriteAsync(srcAddress,alignedSourceNextAddress,alignedDestinationAddress,numBytesToWrite,callback,callbackContext);
        }

        public override void ReadAsync(int segmentId,ulong sourceAddress,IntPtr destinationAddress,uint readLength,DeviceIOCompletionCallback callback,object callbackContext) {
            if(this.ShouldFail) {
                const uint ERROR_CODE = 0xFFFFFFFF;
                callback(ERROR_CODE,readLength,callbackContext);
                return;
            }

            base.ReadAsync(segmentId,sourceAddress,destinationAddress,readLength,callback,callbackContext);
        }
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_LoadAsync_HashSet_データが永続化され復元される() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            token=await store.SaveLogOnlyAsync(ct: ct);
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_LoadAsync_Dictionary_データが永続化され復元される() {
        var ct = TestContext.Current.CancellationToken;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveLogOnlyAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = HashStore.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var dict = store.Create();
            Assert.True(dict.ContainsKey(1));
            Assert.Equal("one",dict[1]);
            Assert.True(dict.ContainsKey(2));
            Assert.Equal("two",dict[2]);
            Assert.False(dict.ContainsKey(99));
        }
    }
}
