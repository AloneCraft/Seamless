using Seamless.Tsavorite.Stores;
using System.Linq;
using System.Runtime.Versioning;
using Tsavorite.core;
namespace Tsavorite.Collection.Shared;

public class StoreCheckpoint {
    //private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,"TsavoriteTest_"+Guid.NewGuid().ToString("N"));
    private static string CreateFolder() => TestPath.Directory<StoreCheckpoint>();
    [BenchFact]
    public async Task SaveAsync_CheckpointDirNotSet_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        using var store = Store.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveAsync(ct: ct));
    }
    [BenchFact]
    public async Task LoadAsync_CheckpointDirNotSet_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        using var store = Store.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.LoadAsync(ct));
    }
    [BenchFact]
    public async Task SaveLogOnlyAsync_CheckpointDirNotSet_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        using var store = Store.Create().Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveLogOnlyAsync(ct: ct));
    }
    [BenchFact,Benchmark]
    public void Save_Load_HashSet_DataPersistsAndRestores() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
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
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
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
    [BenchFact,Benchmark]
    public async Task SaveAsync_LoadAsync_HashSet_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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
    [BenchFact,Benchmark]
    public async Task SaveAsync_LoadAsync_Dictionary_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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
    [BenchFact,Benchmark]
    public async Task SaveAsync_LoadAsync_HashSet_RecoveryIndexRestoresAndContainsWorks() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            using var set = store.Create();
            for(var i = 0; i<100; i++) set.Add(i);
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            for(var i = 0; i<100; i++) Assert.True(set.Contains(i),$"Contains({i}) failed after recovery");
            Assert.False(set.Contains(999));
        }
    }
    [BenchFact,Benchmark]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:郢晏干ﾎ帷ｹ昴・繝ｨ郢晁ｼ斐°郢晢ｽｼ郢晢｣ｰ邵ｺ・ｮ闔蜻磯共隲､・ｧ郢ｧ蜻茨ｽ､諛・ｽｨ・ｼ",Justification = "<闖ｫ譎芽風闕ｳ・ｭ>")]
    public async Task SaveAsync_LoadAsync_Dictionary_RecoveryIndexRestoresAndReadWorks() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            for(var i = 0; i<50; i++) dict.Add(i,$"val_{i}");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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

    [BenchFact,Benchmark]
    public void Save_Load_Dictionary_ExpandsRecoveryIndexSizeByEntryCount() {
        const long initialIndexSize = 64;
        const int entryCount = 100;
        var expectedIndexSize = Store.GetIndexSizeForCapacity(entryCount,initialIndexSize);
        Assert.True(expectedIndexSize>initialIndexSize);

        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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

    [BenchFact,Benchmark]
    public void SaveLogOnly_LoadLatest_Dictionary_ExpandsRecoveryIndexSizeByEntryCount() {
        const long initialIndexSize = 64;
        const int entryCount = 100;
        var expectedIndexSize = Store.GetIndexSizeForCapacity(entryCount,initialIndexSize);
        Assert.True(expectedIndexSize>initialIndexSize);

        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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

    [BenchFact]
    public async Task SaveAsync_CheckpointFailed_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = Devices.CreateLogDevice(folder);
        using var objDevice = Devices.CreateLogDevice(folder+"_obj");
        using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        // SaveAsync 邵ｺ謔滄・隴帶ｺｽ蝎ｪ邵ｺ・ｫ郢晢ｽｭ郢昴・縺醍ｹｧ雋槫徐陟募干・邵ｺ・ｦ邵ｺ荵晢ｽ蛾ｫｱ讒ｫ驟碑ｭ帙・I/O 郢ｧ雋橸ｽｾ繝ｻ・ｩ貅倪・郢ｧ荵昶螺郢ｧ竏堋繝ｻ        // await 邵ｺ蟶吮・郢ｧ・ｿ郢ｧ・ｹ郢ｧ・ｯ郢ｧ蜑・ｽｿ譎・亜邵ｺ蜉ｱ窶ｻ邵ｺ鄙ｫ・･邵ｺ阮吮・邵ｺ・ｧ郢晢ｽｭ郢昴・縺醍ｸｺ謔溷徐陟募干・・ｹｧ蠕娯螺霑･・ｶ隲ｷ荵晢ｽ帝け・ｭ隰問・縲堤ｸｺ髦ｪ・・
        var ongoingCheckpoint = store.SaveAsync(ct: ct).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(ct: ct).AsTask());
        await ongoingCheckpoint;
    }
    [BenchFact]
    public async Task SaveLogOnlyAsync_CheckpointFailed_ThrowsInvalidOperationException() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = Devices.CreateLogDevice(folder);
        using var objDevice = Devices.CreateLogDevice(folder+"_obj");
        using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        // SaveLogOnlyAsync 邵ｺ謔滄・隴帶ｺｽ蝎ｪ邵ｺ・ｫ郢晢ｽｭ郢昴・縺醍ｹｧ雋槫徐陟募干・邵ｺ・ｦ邵ｺ荵晢ｽ蛾ｫｱ讒ｫ驟碑ｭ帙・I/O 郢ｧ雋橸ｽｾ繝ｻ・ｩ貅倪・郢ｧ荵昶螺郢ｧ竏堋繝ｻ        // await 邵ｺ蟶吮・郢ｧ・ｿ郢ｧ・ｹ郢ｧ・ｯ郢ｧ蜑・ｽｿ譎・亜邵ｺ蜉ｱ窶ｻ邵ｺ鄙ｫ・･邵ｺ阮吮・邵ｺ・ｧ郢晢ｽｭ郢昴・縺醍ｸｺ謔溷徐陟募干・・ｹｧ蠕娯螺霑･・ｶ隲ｷ荵晢ｽ帝け・ｭ隰問・縲堤ｸｺ髦ｪ・・
        var ongoingCheckpoint = store.SaveLogOnlyAsync(ct: ct).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveLogOnlyAsync(ct: ct).AsTask());
        await ongoingCheckpoint;
    }
    [BenchFact]
    public async Task SaveAsync_CompleteCheckpointAsyncCanceled_ThrowsOperationCanceledException() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = Devices.CreateLogDevice(folder);
        using var objDevice = Devices.CreateLogDevice(folder+"_obj");
        using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
        using var set = store.Create();
        set.Add(1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(ct: cts.Token).AsTask());
    }
    [SupportedOSPlatform("windows")]
    private class SimulatedFlakyDevice(string path):LocalStorageDevice(path,false,false,false,-1,false,false,false) {
        // 陞滂ｽｱ隰ｨ蜉ｱ・・ｸｺ蟶呻ｽ狗ｸｺ荵昶・邵ｺ繝ｻﾂｰ邵ｺ・ｮ郢ｧ・ｹ郢ｧ・､郢昴・繝｡
        public bool ShouldFail { get; set; } = false;
        // 郢昶・縺臥ｹ昴・縺醍ｹ晄亢縺・ｹ晢ｽｳ郢晉｣ｯﾂ・ｲ髯ｦ蠕｡・ｸ・ｭ霑･・ｶ隲ｷ荵晢ｽ定抄諛奇ｽ狗ｸｺ貅假ｽ∫ｸｺ・ｮ鬩輔・・ｻ・ｶ(ms)
        public int WriteDelayMilliseconds { get; set; } = 0;

        public override void WriteAsync(IntPtr srcAddress,int alignedSourceNextAddress,ulong alignedDestinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object callbackContext) {
            if(ShouldFail) {
                // Complete the callback with an error code to simulate device failure.
                const uint ERROR_CODE = 0xFFFFFFFF;
                callback(ERROR_CODE,numBytesToWrite,callbackContext);
                return;
            }

            if(WriteDelayMilliseconds>0)
                Thread.Sleep(WriteDelayMilliseconds);

            // Use the normal device path when failure is disabled.
            base.WriteAsync(srcAddress,alignedSourceNextAddress,alignedDestinationAddress,numBytesToWrite,callback,callbackContext);
        }

        public override void ReadAsync(int segmentId,ulong sourceAddress,IntPtr destinationAddress,uint readLength,DeviceIOCompletionCallback callback,object callbackContext) {
            if(ShouldFail) {
                const uint ERROR_CODE = 0xFFFFFFFF;
                callback(ERROR_CODE,readLength,callbackContext);
                return;
            }

            base.ReadAsync(segmentId,sourceAddress,destinationAddress,readLength,callback,callbackContext);
        }
    }
    [BenchFact,Benchmark]
    public async Task SaveLogOnlyAsync_LoadAsync_HashSet_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
    [BenchFact,Benchmark]
    public async Task SaveLogOnlyAsync_LoadAsync_Dictionary_DataPersistsAndRestores() {
        var ct = CancellationToken.None;
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveLogOnlyAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
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
