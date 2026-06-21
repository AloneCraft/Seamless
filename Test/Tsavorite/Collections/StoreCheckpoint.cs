using Tsavorite.Stores;
using System.Linq;
using System.Runtime.Versioning;
using Tsavorite.core;
namespace Test.Tsavorite.Collections;

public class StoreCheckpoint {
    //private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,"TsavoriteTest_"+Guid.NewGuid().ToString("N"));
    private static string CreateFolder() => TestPath.Directory<StoreCheckpoint>();
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_CheckpointDirNotSet_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        using var store = Store.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveAsync(ct: ct));
    }
    [Fact, Trait("Category","Unit")]
    public async Task LoadAsync_CheckpointDirNotSet_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        using var store = Store.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.LoadAsync(ct));
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_CheckpointDirNotSet_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        using var store = Store.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveLogOnlyAsync(ct: ct));
    }
    [Fact, Trait("Category","Unit")]
    public void Save_Load_HashSet_DataPersistsAndRestores() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var LogObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            using var set = store.Create();
            set[1]=11;
            set[2]=22;
            set[3]=33;
            Assert.True(set.Contains(1));
            token=store.Save();
            Assert.True(set.Contains(1));
        }
        {
            using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var LogObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
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
    public async Task SaveAsync_LoadAsync_HashSet_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            token=await store.SaveAsync(ct: ct);
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
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
    public async Task SaveAsync_LoadAsync_Dictionary_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
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
    public async Task SaveAsync_LoadAsync_HashSet_RecoveryIndexRestoresAndContainsWorks() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            for(var i = 0; i<100; i++) set.Add(i);
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            for(var i = 0; i<100; i++) Assert.True(set.Contains(i),$"Contains({i}) failed after recovery");
            Assert.False(set.Contains(999));
        }
    }
    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:繝励Λ繝・ヨ繝輔か繝ｼ繝縺ｮ莠呈鋤諤ｧ繧呈､懆ｨｼ",Justification = "<菫晉蕗荳ｭ>")]
    public async Task SaveAsync_LoadAsync_Dictionary_RecoveryIndexRestoresAndReadWorks() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            for(var i = 0; i<50; i++) dict.Add(i,$"val_{i}");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
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
    public void Save_Load_Dictionary_ExpandsRecoveryIndexSizeByEntryCount() {
        const long initialIndexSize = 64;
        const int entryCount = 100;
        var expectedIndexSize = Store.GetIndexSizeForCapacity(entryCount,initialIndexSize);
        Assert.True(expectedIndexSize>initialIndexSize);

        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            var factory = Store.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
            using var store = factory.Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();

            for(var i = 0; i<entryCount; i++)
                dict.Add(i,$"val_{i}");

            token=store.Save();
        }

        var recoveryFactory = Store.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
        Assert.Equal(expectedIndexSize,recoveryFactory.ResolveRecoveryIndexSize(token,initialIndexSize));

        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
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
    public void SaveLogOnly_LoadLatest_Dictionary_ExpandsRecoveryIndexSizeByEntryCount() {
        const long initialIndexSize = 64;
        const int entryCount = 100;
        var expectedIndexSize = Store.GetIndexSizeForCapacity(entryCount,initialIndexSize);
        Assert.True(expectedIndexSize>initialIndexSize);

        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            var factory = Store.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
            using var store = factory.Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();

            for(var i = 0; i<entryCount; i++)
                dict.Add(i,$"val_{i}");

            _=store.SaveLogOnly();
        }

        var recoveryFactory = Store.Create(IndexSize: initialIndexSize,CheckpointDir: checkpointDir);
        Assert.Equal(expectedIndexSize,recoveryFactory.ResolveRecoveryIndexSize(minimumIndexSize: initialIndexSize));

        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
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
    public async Task SaveAsync_CheckpointFailed_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
        using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        // SaveAsync 縺悟酔譛溽噪縺ｫ繝ｭ繝・け繧貞叙蠕励＠縺ｦ縺九ｉ髱槫酔譛・I/O 繧貞ｾ・ｩ溘☆繧九◆繧√・        // await 縺帙★繧ｿ繧ｹ繧ｯ繧剃ｿ晄戟縺励※縺翫￥縺薙→縺ｧ繝ｭ繝・け縺悟叙蠕励＆繧後◆迥ｶ諷九ｒ邯ｭ謖√〒縺阪ｋ
        var ongoingCheckpoint = store.SaveAsync(ct: ct).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(ct: ct).AsTask());
        await ongoingCheckpoint;
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_CheckpointFailed_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
        using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        // SaveLogOnlyAsync 縺悟酔譛溽噪縺ｫ繝ｭ繝・け繧貞叙蠕励＠縺ｦ縺九ｉ髱槫酔譛・I/O 繧貞ｾ・ｩ溘☆繧九◆繧√・        // await 縺帙★繧ｿ繧ｹ繧ｯ繧剃ｿ晄戟縺励※縺翫￥縺薙→縺ｧ繝ｭ繝・け縺悟叙蠕励＆繧後◆迥ｶ諷九ｒ邯ｭ謖√〒縺阪ｋ
        var ongoingCheckpoint = store.SaveLogOnlyAsync(ct: ct).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveLogOnlyAsync(ct: ct).AsTask());
        await ongoingCheckpoint;
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveAsync_CompleteCheckpointAsyncCanceled_ThrowsOperationCanceledException() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
        using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(ct: cts.Token).AsTask());
    }
    [SupportedOSPlatform("windows")]
    private class SimulatedFlakyDevice(string path):LocalStorageDevice(path,false,false,false,-1,false,false,false) {
        // 螟ｱ謨励＆縺帙ｋ縺九←縺・°縺ｮ繧ｹ繧､繝・メ
        public bool ShouldFail { get; set; } = false;
        // 繝√ぉ繝・け繝昴う繝ｳ繝磯ｲ陦御ｸｭ迥ｶ諷九ｒ菴懊ｋ縺溘ａ縺ｮ驕・ｻｶ(ms)
        public int WriteDelayMilliseconds { get; set; } = 0;

        public override void WriteAsync(IntPtr srcAddress,int alignedSourceNextAddress,ulong alignedDestinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object callbackContext) {
            if(this.ShouldFail) {
                // Complete the callback with an error code to simulate device failure.
                const uint ERROR_CODE = 0xFFFFFFFF;
                callback(ERROR_CODE,numBytesToWrite,callbackContext);
                return;
            }

            if(this.WriteDelayMilliseconds>0)
                Thread.Sleep(this.WriteDelayMilliseconds);

            // Use the normal device path when failure is disabled.
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
    public async Task SaveLogOnlyAsync_LoadAsync_HashSet_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            token=await store.SaveLogOnlyAsync(ct: ct);
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
    [Fact, Trait("Category","Unit")]
    public async Task SaveLogOnlyAsync_LoadAsync_Dictionary_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveLogOnlyAsync(ct: ct);
        }
        {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
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
