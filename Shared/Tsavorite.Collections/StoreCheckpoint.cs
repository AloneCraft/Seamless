using System.Runtime.Versioning;

using Tsavorite.core;
namespace Shared.Tsavorite.Collections;

public class StoreCheckpoint {
    private static string CreateFolder([System.Runtime.CompilerServices.CallerMemberName]string testName=""){
        var root=TestPath.Directory(nameof(StoreCheckpoint),testName);
        return Path.Combine(root,"store");
    }
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
    [BenchFact, Benchmark]
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
    [BenchFact, Benchmark]
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
    [BenchFact, Benchmark]
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
    [BenchFact, Benchmark]
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
            for(var i = 0;i<100;i++) set.Add(i);
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var set = store.Create();
            for(var i = 0;i<100;i++) Assert.True(set.Contains(i),$"Contains({i}) failed after recovery");
            Assert.False(set.Contains(999));
        }
    }
    [BenchFact, Benchmark]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:Validate platform compatibility",Justification = "Cross-platform test fixture")]
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
            for(var i = 0;i<50;i++) dict.Add(i,$"val_{i}");
            token=await store.SaveAsync(ct: ct);
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,string>(logDevice,objDevice);
            await store.LoadAsync(token,ct);
            using var dict = store.Create();
            for(var i = 0;i<50;i++) {
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
            Assert.False(dict.ContainsKey(999));
        }
    }

    [BenchFact, Benchmark]
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

            for(var i = 0;i<entryCount;i++)
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
            for(var i = 0;i<entryCount;i++) {
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after resize-aware recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
        }
    }

    [BenchFact, Benchmark]
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

            for(var i = 0;i<entryCount;i++)
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
            for(var i = 0;i<entryCount;i++) {
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
        // SaveAsync の完了前に再実行すると InvalidOperationException になることを確認する。
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
        // SaveLogOnlyAsync の完了前に再実行すると InvalidOperationException になることを確認する。
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
        // true の場合は書き込み/読み込みで擬似的に失敗させる。
        public bool ShouldFail { get; set; } = false;
        // テストで遅延を再現するための待機時間 (ms)。
        public int WriteDelayMilliseconds { get; set; } = 0;

        public override void WriteAsync(IntPtr srcAddress,int alignedSourceNextAddress,ulong alignedDestinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object callbackContext) {
            if(this.ShouldFail) {
                // Complete the callback with an error code to simulate device failure.
                const uint ERROR_CODE = 0xFFFFFFFF;
                callback(ERROR_CODE,numBytesToWrite,callbackContext,null);
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
                callback(ERROR_CODE,readLength,callbackContext,null);
                return;
            }

            base.ReadAsync(segmentId,sourceAddress,destinationAddress,readLength,callback,callbackContext);
        }
    }
    [BenchFact, Benchmark]
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
    [BenchFact, Benchmark]
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
    [BenchFact, Benchmark]
    public void Flush_AfterSave_DataPersistsAndRestores() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(logDevice,objDevice);
            using var set = store.Create();
            set[1]=11;
            set[2]=22;
            // 保存する前は書き出すチェックポイントが無い(ログのデバイスのファイルだけ)。失敗しない。
            store.Flush();
            token=store.Save();
            // デバイスが開いたままでも書き出せる。書き出したあとに続けて呼んでも失敗しない。
            store.Flush();
            store.Flush();
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(logDevice,objDevice);
            store.Load(token);
            using var set = store.Create();
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.False(set.Contains(99));
        }
    }
    // ログの末尾がページの境界ちょうどで、メモリのバッファが満杯になるまで 1 件ずつ追加する。
    // Garnet 2.0.1-beta.6 以降(2.1.8 でも確認)の素の復元は、この状態で保存したチェックポイントで先頭ページの内容を失う(CollectionStore.RecoverWithSizeTrackerAsync で防ぐ)。
    private static long AddUntilTailAtPageBoundaryWithFullBuffer(HashSetStore<long> store,global::Seamless.Collections.Tsavorite.HashSet<long> set) {
        const long pageSize = 4096;
        const long memorySize = 65536;
        long count = 0;
        while(true) {
            set.Add(count++);
            var tail = store.Store.Log.TailAddress;
            if(tail%pageSize==0&&tail>=memorySize) {
                return count;
            }
            Assert.True(count<100_000,"ログの末尾がページ境界に来なかった");
        }
    }
    [BenchFact, Benchmark]
    public void Save_TailAtPageBoundary_DataPersistsAndRestores() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        long count;
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(MemorySize: 65536,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
            using var set = store.Create();
            count=AddUntilTailAtPageBoundaryWithFullBuffer(store,set);
            token=store.Save();
        }
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(MemorySize: 65536,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
            store.Load(token);
            using var set = store.Create();
            var missing = Enumerable.Range(0,(int)count).Count(i => !set.Contains(i));
            Assert.Equal(0,missing);
        }
    }
    [BenchFact, Benchmark]
    public void Load_ReopenedWithSmallerMemory_DataPersistsAndRestores() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(MemorySize: 65536,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
            using var set = store.Create();
            // 16 ページのバッファが満杯になるまで追加して保存する(境界は避ける)。
            for(long i = 0;i<2100;i++) {
                set.Add(i);
            }
            token=store.Save();
        }
        {
            // 保存時の半分のメモリで開き直しても、すべて復元できる。Garnet 2.1.8 の素の復元は 8 ページ分の内容を失う(2.0.1-beta.5 は失わない)。
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(MemorySize: 32768,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
            store.Load(token);
            using var set = store.Create();
            Assert.Equal(0,Enumerable.Range(0,2100).Count(i => !set.Contains(i)));
        }
        {
            // 同じメモリで開き直せば、すべて復元できる。
            using var logDevice = Devices.CreateLogDevice(folder);
            using var objDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(MemorySize: 65536,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
            store.Load(token);
            using var set = store.Create();
            Assert.Equal(0,Enumerable.Range(0,2100).Count(i => !set.Contains(i)));
        }
    }
    [BenchFact]
    public void Flush_MemoryStore_DoesNothing() {
        using var store = Store.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        // ディスクを使わないストアには書き出すものが無い。
        store.Flush();
    }
    [BenchFact]
    public void Flush_Disposed_ThrowsObjectDisposedException() {
        var store = Store.Create().Create<int>();
        store.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store.Flush());
    }
}
