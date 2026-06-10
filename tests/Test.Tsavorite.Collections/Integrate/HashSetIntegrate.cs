using Tsavorite.Collections;
using Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Tsavorite.Integrate;

/// <summary>
/// HashSet (Key) のセグメント管理・チェックポイントテストの基底クラス。
/// MutableFractionを最小化（0.05）、SegmentSizeも最小化（PageSize）して、
/// セグメントファイルがどんどん作成される様子をテストする。
/// </summary>
public abstract class HashSetIntegrateBase<TKey>(ITestOutputHelper output)
    where TKey : IEquatable<TKey> {
    private readonly ITestOutputHelper output = output;

    protected abstract string TestClassName { get; }
    protected abstract TKey MakeKey(int i);
    protected abstract Handle CreateHandle(string rootDir,string? checkpointDir = null,bool deleteOnDispose = true);
    protected virtual int SegmentsCreatedDataSize => 5000;
    protected virtual int SegmentsCreatedCheckInterval => 500;
    protected virtual int SegmentGrowthDataSize => 300;
    protected virtual int SegmentGrowthCheckInterval => 300;
    protected virtual int SegmentSteadyStateDataSize => 20000;
    protected virtual int SegmentSteadyStateCheckInterval => 1000;
    protected virtual int SegmentSteadyStateStableWindow => 3;
    protected virtual int SegmentSteadyStateWorkingSetSize => 1024;
    protected virtual int SegmentSizeBytes => 4096;
    protected virtual int CheckpointDataSize => 200;
    protected virtual int RefreshDataSize => 4096;
    protected virtual int RefreshWorkingSetSize => 128;

    private string TestDir(string methodName) =>
        TestPath.Directory(this.TestClassName,methodName);

    protected abstract class Handle:IDisposable {
        protected readonly string rootDir;
        protected readonly IDevice logDevice;
        protected readonly IDevice objDevice;
        private readonly bool deleteOnDispose;

        protected Handle(string rootDir,bool deleteOnDispose = true) {
            this.rootDir=rootDir;
            this.deleteOnDispose=deleteOnDispose;
            Directory.CreateDirectory(rootDir);
            this.logDevice=Devices.CreateLogDevice(Path.Combine(rootDir,"log"),deleteOnClose: false);
            this.objDevice=Devices.CreateLogDevice(Path.Combine(rootDir,"obj"),deleteOnClose: false);
        }

        public int CountSegmentFiles() => Directory.GetFiles(this.rootDir,"log.*").Length;
        public virtual void Flush() { }
        public virtual void FlushAndEvict() { }
        public virtual void Compact() { }
        public virtual long GetBeginAddress() => 0;
        public virtual long GetTailAddress() => 0;
        public abstract IHashSet<TKey> Create();
        public abstract Guid Save();
        public abstract void Load(Guid token);
        protected abstract void DisposeStore();

        public void Dispose() {
            this.DisposeStore();
            this.logDevice.Dispose();
            this.objDevice.Dispose();
            if(this.deleteOnDispose) try { Directory.Delete(this.rootDir,true); } catch { }
        }
    }

    private int GetActiveSegmentCount(Handle handle) {
        var beginAddress = handle.GetBeginAddress();
        var tailAddress = handle.GetTailAddress();
        var bytesInUse = Math.Max(0,tailAddress-beginAddress);
        var segmentCount = (bytesInUse+this.SegmentSizeBytes-1)/this.SegmentSizeBytes;
        return Math.Max(1,(int)segmentCount);
    }

    [Fact]
    public void データ追加中にセグメントファイルが増加することを確認する() {
        var storeDir = this.TestDir(nameof(データ追加中にセグメントファイルが増加することを確認する));
        using var handle = this.CreateHandle(storeDir);
        using var hashset = handle.Create();

        var initialSegmentCount = handle.CountSegmentFiles();
        this.output.WriteLine($"[Initial] Segment files: {initialSegmentCount}");

        try {
            for(int i = 0; i<this.SegmentsCreatedDataSize; i++) {
                hashset.Add(this.MakeKey(i));

                if((i+1)%this.SegmentsCreatedCheckInterval==0) {
                    var segmentCount = handle.CountSegmentFiles();
                    this.output.WriteLine($"[After {i+1} adds] Segment files: {segmentCount}");
                }
            }

            var finalSegmentCount = handle.CountSegmentFiles();
            this.output.WriteLine($"[Final] Segment files: {finalSegmentCount}");
            this.output.WriteLine($"✓ Segments created: {finalSegmentCount} files");

            Assert.True(finalSegmentCount>initialSegmentCount,
                $"Expected more segments than initial ({initialSegmentCount}), but got {finalSegmentCount}");
        }
        finally { }
    }

    [Fact]
    public void セグメント数がデータ追加に伴い単調増加することを確認する() {
        var storeDir = this.TestDir(nameof(セグメント数がデータ追加に伴い単調増加することを確認する));
        using var handle = this.CreateHandle(storeDir);
        using var hashset = handle.Create();

        var segmentCounts = new global::System.Collections.Generic.List<int>();
        var addCountAtSnapshots = new global::System.Collections.Generic.List<int>();

        try {
            for(int i = 0; i<this.SegmentGrowthDataSize; i++) {
                hashset.Add(this.MakeKey(i));

                if((i+1)%this.SegmentGrowthCheckInterval==0) {
                    var count = handle.CountSegmentFiles();
                    segmentCounts.Add(count);
                    addCountAtSnapshots.Add(i+1);
                    this.output.WriteLine($"[{i+1} adds] Segments: {count}");
                }
            }

            for(int i = 1; i<segmentCounts.Count; i++) {
                Assert.True(segmentCounts[i]>=segmentCounts[i-1],
                    $"Segment count decreased at iteration {i}: {segmentCounts[i-1]} -> {segmentCounts[i]}");
            }
            this.output.WriteLine($"✓ Segment growth is progressive: {string.Join(" -> ",segmentCounts)} files");
        }
        finally { }
    }

    [Fact]
    public void 追加と削除を繰り返すとコンパクションで利用セグメント範囲の増え方を抑えられることを確認する() {
        Assert.True(this.SegmentSteadyStateDataSize>this.SegmentSteadyStateWorkingSetSize,
            $"Expected SegmentSteadyStateDataSize ({this.SegmentSteadyStateDataSize}) to exceed working set size ({this.SegmentSteadyStateWorkingSetSize})");

        (int SegmentFiles,int ActiveSegments,long BeginAddress,int Count) RunScenario(string scenarioName,bool compact) {
            var storeDir = this.TestDir($"{nameof(追加と削除を繰り返すとコンパクションで利用セグメント範囲の増え方を抑えられることを確認する)}_{scenarioName}");
            using var handle = this.CreateHandle(storeDir);
            using var hashset = handle.Create();

            for(int i = 0; i<this.SegmentSteadyStateWorkingSetSize; i++) {
                hashset.Add(this.MakeKey(i));
            }

            for(int i = this.SegmentSteadyStateWorkingSetSize; i<this.SegmentSteadyStateDataSize; i++) {
                hashset.Add(this.MakeKey(i));
                Assert.True(hashset.Remove(this.MakeKey(i-this.SegmentSteadyStateWorkingSetSize)),
                    $"Expected key {this.MakeKey(i-this.SegmentSteadyStateWorkingSetSize)} to exist before removal");
            }

            if(compact) {
                handle.Compact();
            }
            handle.Flush();

            var segmentFiles = handle.CountSegmentFiles();
            var activeSegments = this.GetActiveSegmentCount(handle);
            var beginAddress = handle.GetBeginAddress();
            this.output.WriteLine($"[{scenarioName}] Segment files: {segmentFiles}, Active segments: {activeSegments}, BeginAddress: {beginAddress}, Count: {hashset.Count}");

            return (segmentFiles,activeSegments,beginAddress,hashset.Count);
        }

        var withoutCompaction = RunScenario("without_compaction",compact: false);
        var withCompaction = RunScenario("with_compaction",compact: true);

        Assert.Equal(this.SegmentSteadyStateWorkingSetSize,withoutCompaction.Count);
        Assert.Equal(this.SegmentSteadyStateWorkingSetSize,withCompaction.Count);
        Assert.True(withCompaction.BeginAddress>withoutCompaction.BeginAddress,
            $"Expected compaction to advance BeginAddress beyond no-compaction case, but got {withoutCompaction.BeginAddress} vs {withCompaction.BeginAddress}");
        Assert.True(withCompaction.ActiveSegments<withoutCompaction.ActiveSegments,
            $"Expected compaction to reduce active segment growth, but got {withoutCompaction.ActiveSegments} vs {withCompaction.ActiveSegments}");
    }

    [Fact]
    public void 複数HashSetで未Refreshセッションが残る間は古いログセグメントが削除されず全てRefreshすると削除される() {
        var storeDir = this.TestDir(nameof(複数HashSetで未Refreshセッションが残る間は古いログセグメントが削除されず全てRefreshすると削除される));
        try {
            using var handle = this.CreateHandle(storeDir);
            using var owner = handle.Create();
            using var sibling1 = handle.Create();
            using var sibling2 = handle.Create();

            for(int i = 0; i<this.RefreshWorkingSetSize; i++) {
                Assert.True(owner.Add(this.MakeKey(i)),$"Expected initial add for key {this.MakeKey(i)} to succeed");
            }
            for(int i = 0; i<this.RefreshWorkingSetSize; i++) {
                Assert.True(sibling1.Contains(this.MakeKey(i)),$"Expected sibling1 to observe key {this.MakeKey(i)} before churn");
                Assert.True(sibling2.Contains(this.MakeKey(i)),$"Expected sibling2 to observe key {this.MakeKey(i)} before churn");
            }

            for(int i = this.RefreshWorkingSetSize; i<this.RefreshDataSize; i++) {
                Assert.True(owner.Add(this.MakeKey(i)),$"Expected add for key {this.MakeKey(i)} to succeed during churn");
                Assert.True(owner.Remove(this.MakeKey(i-this.RefreshWorkingSetSize)),
                    $"Expected key {this.MakeKey(i-this.RefreshWorkingSetSize)} to exist before removal during churn");
            }

            var liveKey = this.MakeKey(this.RefreshDataSize-1);

            owner.Refresh();
            sibling1.Refresh();
            Assert.True(owner.Contains(liveKey),$"Expected owner to observe live key {liveKey} after partial refresh");
            Assert.True(sibling1.Contains(liveKey),$"Expected sibling1 to observe live key {liveKey} after partial refresh");

            handle.Flush();
            handle.Compact();
            handle.FlushAndEvict();

            var beginAddressWithStaleSibling = handle.GetBeginAddress();
            var segmentFilesWithStaleSibling = handle.CountSegmentFiles();
            this.output.WriteLine($"[with_stale_sibling] BeginAddress after compact: {beginAddressWithStaleSibling}, Segment files: {segmentFilesWithStaleSibling}");

            sibling2.Refresh();
            Assert.True(sibling2.Contains(liveKey),$"Expected sibling2 to observe live key {liveKey} after final refresh");

            handle.Flush();
            handle.Compact();
            handle.FlushAndEvict();

            var beginAddressAfterAllRefresh = handle.GetBeginAddress();
            var segmentFilesAfterAllRefresh = handle.CountSegmentFiles();
            this.output.WriteLine($"[after_all_refresh] BeginAddress after compact: {beginAddressAfterAllRefresh}, Segment files: {segmentFilesAfterAllRefresh}");

            Assert.True(beginAddressAfterAllRefresh>beginAddressWithStaleSibling,
                $"Expected final Refresh to advance BeginAddress further, but got {beginAddressWithStaleSibling} vs {beginAddressAfterAllRefresh}");
        }
        finally {
            try { Directory.Delete(storeDir,true); } catch { }
        }
    }

    [Fact]
    public void 複数HashSetでRefresh後も共有ストアのデータを継続参照できる() {
        var storeDir = this.TestDir(nameof(複数HashSetでRefresh後も共有ストアのデータを継続参照できる));
        using var handle = this.CreateHandle(storeDir);
        using var owner = handle.Create();
        using var sibling1 = handle.Create();
        using var sibling2 = handle.Create();

        var count = Math.Min(32,this.RefreshWorkingSetSize);
        for(int i = 0; i<count; i++) {
            Assert.True(owner.Add(this.MakeKey(i)),$"Expected initial add for key {this.MakeKey(i)} to succeed");
        }

        for(int i = 0; i<count; i++) {
            Assert.True(sibling1.Contains(this.MakeKey(i)),$"Expected sibling1 to observe key {this.MakeKey(i)} before refresh");
            Assert.True(sibling2.Contains(this.MakeKey(i)),$"Expected sibling2 to observe key {this.MakeKey(i)} before refresh");
        }

        owner.Refresh();
        sibling1.Refresh();
        sibling2.Refresh();

        for(int i = 0; i<count; i++) {
            Assert.True(owner.Contains(this.MakeKey(i)),$"Expected owner to observe key {this.MakeKey(i)} after refresh");
            Assert.True(sibling1.Contains(this.MakeKey(i)),$"Expected sibling1 to observe key {this.MakeKey(i)} after refresh");
            Assert.True(sibling2.Contains(this.MakeKey(i)),$"Expected sibling2 to observe key {this.MakeKey(i)} after refresh");
        }

        var extraKey = this.MakeKey(count+1);
        Assert.True(owner.Add(extraKey),$"Expected add for key {extraKey} after refresh to succeed");
        Assert.True(sibling1.Contains(extraKey),$"Expected sibling1 to observe key {extraKey} after refresh");
        Assert.True(sibling2.Contains(extraKey),$"Expected sibling2 to observe key {extraKey} after refresh");
        Assert.True(owner.Remove(extraKey),$"Expected remove for key {extraKey} after refresh to succeed");
    }

    [Fact]
    public void チェックポイントと復元() {
        var storeDir = this.TestDir(nameof(チェックポイントと復元));
        var checkpointDir = Path.Combine(storeDir,"checkpoint");
        Directory.CreateDirectory(storeDir);
        Directory.CreateDirectory(checkpointDir);

        var dataSize = this.CheckpointDataSize;
        Guid token = default;

        // データを書き込んでチェックポイントを保存
        {
            var handle = this.CreateHandle(storeDir,checkpointDir,deleteOnDispose: false);
            try {
                using var hashset = handle.Create();
                for(int i = 0; i<dataSize; i++)
                    hashset.Add(this.MakeKey(i));
                token=handle.Save();
                this.output.WriteLine($"✓ Checkpoint saved: {token}");
            }
            finally {
                handle.Dispose();
            }
        }

        // チェックポイントから復元してデータを検証
        {
            var handle = this.CreateHandle(storeDir,checkpointDir,deleteOnDispose: false);
            try {
                handle.Load(token);
                using var hashset = handle.Create();
                for(int i = 0; i<dataSize; i++)
                    Assert.True(hashset.Contains(this.MakeKey(i)),$"Key {this.MakeKey(i)} not found after restore");
                this.output.WriteLine($"✓ All {dataSize} entries verified after restore");
            }
            finally {
                handle.Dispose();
            }
        }
        try { Directory.Delete(storeDir,true); } catch { }
    }
}

/// <summary>
/// ManagementHashSet: Tsavorite HashSet (Managed Key) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class ManagementHashSet(ITestOutputHelper output):HashSetIntegrateBase<string>(output) {
    protected override string TestClassName => "Test.Tsavorite.Integrate.ManagementHashSet";
    protected override string MakeKey(int i) => $"hashset_key_{i:D6}_"+new string('a',50);
    protected override int SegmentsCreatedDataSize => 10000;
    protected override int SegmentsCreatedCheckInterval => 1000;
    protected override int SegmentGrowthDataSize => 5000;
    protected override int SegmentGrowthCheckInterval => 500;
    protected override int SegmentSteadyStateDataSize => 1024;
    protected override int SegmentSteadyStateCheckInterval => 256;
    protected override int SegmentSteadyStateWorkingSetSize => 64;
    protected override int RefreshDataSize => 2048;
    protected override int RefreshWorkingSetSize => 64;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir = null,bool deleteOnDispose = true) =>
        new ManagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class ManagedHandle:Handle {
        private readonly HashSetStore<string> store;

        public ManagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory = HashStore.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<string>(this.logDevice,this.objDevice,new MemoryPackSerializerStrategy());
        }

        public override IHashSet<string> Create() =>
            this.store.Create();
        public override void Flush() =>
            this.store.Store.Log.Flush(wait: true);
        public override void FlushAndEvict() =>
            this.store.Store.Log.FlushAndEvict(wait: true);
        public override void Compact() {
            this.store.Store.Log.ShiftReadOnlyAddress(this.store.Store.Log.TailAddress,true);
            this.store.CompactWithLookup();
        }
        public override long GetBeginAddress() =>
            this.store.Address.BeginAddress;
        public override long GetTailAddress() =>
            this.store.Store.Log.TailAddress;
        public override Guid Save() =>
            this.store.Save();
        public override void Load(Guid token) =>
            this.store.Load(token);

        protected override void DisposeStore() {
            this.store.Store.Log.FlushAndEvict(wait: true);
            this.store.Dispose();
        }
    }
}

/// <summary>
/// UnmanagementHashSet: Tsavorite HashSet (Unmanaged Key) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class UnmanagementHashSet(ITestOutputHelper output):HashSetIntegrateBase<int>(output) {
    protected override string TestClassName => "Test.Tsavorite.Integrate.UnmanagementHashSet";
    protected override int MakeKey(int i) => i;
    protected override int SegmentSteadyStateDataSize => 2048;
    protected override int SegmentSteadyStateCheckInterval => 256;
    protected override int SegmentSteadyStateWorkingSetSize => 128;
    protected override int CheckpointDataSize => 500;
    protected override int RefreshDataSize => 4096;
    protected override int RefreshWorkingSetSize => 128;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir = null,bool deleteOnDispose = true) =>
        new UnmanagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class UnmanagedHandle:Handle {
        private readonly HashSetStore<int> store;

        public UnmanagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory = HashStore.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<int>(this.logDevice,this.objDevice,new BlittableStrategy());
        }

        public override IHashSet<int> Create() =>
            this.store.Create();
        public override void Flush() =>
            this.store.Store.Log.Flush(wait: true);
        public override void FlushAndEvict() =>
            this.store.Store.Log.FlushAndEvict(wait: true);
        public override void Compact() {
            this.store.Store.Log.ShiftReadOnlyAddress(this.store.Store.Log.TailAddress,true);
            this.store.CompactWithLookup();
        }
        public override long GetBeginAddress() =>
            this.store.Address.BeginAddress;
        public override long GetTailAddress() =>
            this.store.Store.Log.TailAddress;
        public override Guid Save() =>
            this.store.Save();
        public override void Load(Guid token) =>
            this.store.Load(token);

        protected override void DisposeStore() {
            this.store.Store.Log.FlushAndEvict(wait: true);
            this.store.Dispose();
        }
    }
}
