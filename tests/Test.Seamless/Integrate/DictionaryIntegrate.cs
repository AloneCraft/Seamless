using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Integrate;

/// <summary>
/// Dictionary (Key × Value) のセグメント管理・チェックポイントテストの基底クラス。
/// MutableFractionを最小化（0.05）、SegmentSizeも最小化（PageSize）して、
/// セグメントファイルがどんどん作成される様子をテストする。
/// </summary>
public abstract class DictionaryIntegrateBase<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private readonly ITestOutputHelper output;

    protected DictionaryIntegrateBase(ITestOutputHelper output) {
        this.output=output;
    }

    protected abstract string TestClassName { get; }
    protected abstract TKey MakeKey(int i);
    protected abstract TValue MakeValue(int i);
    protected virtual TValue MakeSegmentCreatedValue(int i) =>
        this.MakeValue(i);
    protected abstract Handle CreateHandle(string rootDir,string? checkpointDir = null,bool deleteOnDispose = true);
    protected virtual int SegmentsCreatedMaxInserts => 5000;
    protected virtual int SegmentsCreatedCheckInterval => 500;
    protected virtual bool SegmentsCreatedBreakOnFirstGrowth => false;
    protected virtual int SegmentGrowthDataSize => 5000;
    protected virtual int SegmentGrowthCheckInterval => 500;
    protected virtual int CheckpointDataSize => 200;
    protected virtual int RefreshDataSize => 4096;
    protected virtual int RefreshWorkingSetSize => 128;

    private string TestDir(string methodName) =>
        global::Test.Seamless.TestPath.Directory(this.TestClassName,methodName);

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
        /// <summary>SegmentsCreatedDuringDataInsertion でのチェック前フラッシュ。既定は no-op。</summary>
        public virtual void Flush() { }
        public virtual void FlushAndEvict() { }
        public virtual void Compact() { }
        public virtual long GetBeginAddress() => 0;
        public abstract ISeamlessDictionary<TKey,TValue> Create();
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

    [Fact]
    public void データ挿入中にセグメントファイルが増加することを確認する() {
        var storeDir = this.TestDir(nameof(this.データ挿入中にセグメントファイルが増加することを確認する));
        using var handle = this.CreateHandle(storeDir);
        using var dict = handle.Create();

        var initialSegmentCount = handle.CountSegmentFiles();
        this.output.WriteLine($"[Initial] Segment files: {initialSegmentCount}");

        var firstGrowthAt = -1;
        try {
            for(int i = 0; i<this.SegmentsCreatedMaxInserts; i++) {
                dict.Add(this.MakeKey(i),this.MakeSegmentCreatedValue(i));

                if((i+1)%this.SegmentsCreatedCheckInterval==0) {
                    handle.Flush();
                    var segmentCount = handle.CountSegmentFiles();
                    this.output.WriteLine($"[After {i+1} inserts] Segment files: {segmentCount}");
                    if(this.SegmentsCreatedBreakOnFirstGrowth&&segmentCount>initialSegmentCount) {
                        firstGrowthAt=i+1;
                        break;
                    }
                }
            }

            var finalSegmentCount = handle.CountSegmentFiles();
            this.output.WriteLine($"[Final] Segment files: {finalSegmentCount}");
            if(firstGrowthAt>=0)
                this.output.WriteLine($"✓ First segment growth observed at {firstGrowthAt} inserts");
            this.output.WriteLine($"✓ Segments created: {finalSegmentCount} files");

            Assert.True(finalSegmentCount>initialSegmentCount,
                $"Expected more segments than initial ({initialSegmentCount}), but got {finalSegmentCount} after {this.SegmentsCreatedMaxInserts} inserts");
        }
        finally { }
    }

    [Fact]
    public void セグメント数がデータ挿入に伴い単調増加することを確認する() {
        var storeDir = this.TestDir(nameof(this.セグメント数がデータ挿入に伴い単調増加することを確認する));
        using var handle = this.CreateHandle(storeDir);
        using var dict = handle.Create();

        var segmentCounts = new global::System.Collections.Generic.List<int>();
        var insertCountAtSnapshots = new global::System.Collections.Generic.List<int>();

        try {
            for(int i = 0; i<this.SegmentGrowthDataSize; i++) {
                dict.Add(this.MakeKey(i),this.MakeValue(i));

                if((i+1)%this.SegmentGrowthCheckInterval==0) {
                    var count = handle.CountSegmentFiles();
                    segmentCounts.Add(count);
                    insertCountAtSnapshots.Add(i+1);
                    this.output.WriteLine($"[{i+1} inserts] Segments: {count}");
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

    [Fact(Skip = "Long-running Tsavorite truncation observation test. Run manually when investigating Refresh/truncation behavior.")]
    public void 複数Dictionaryで未Refreshセッションが残る間は古いログセグメントが削除されず全てRefreshすると削除される() {
        var storeDir = this.TestDir(nameof(this.複数Dictionaryで未Refreshセッションが残る間は古いログセグメントが削除されず全てRefreshすると削除される));
        try {
            using var handle = this.CreateHandle(storeDir);
            using var owner = handle.Create();
            using var sibling1 = handle.Create();
            using var sibling2 = handle.Create();

            for(int i = 0; i<this.RefreshWorkingSetSize; i++) {
                owner.Add(this.MakeKey(i),this.MakeSegmentCreatedValue(i));
            }
            for(int i = 0; i<this.RefreshWorkingSetSize; i++) {
                Assert.True(sibling1.TryGetValue(this.MakeKey(i),out _),$"Expected sibling1 to observe key {this.MakeKey(i)} before churn");
                Assert.True(sibling2.TryGetValue(this.MakeKey(i),out _),$"Expected sibling2 to observe key {this.MakeKey(i)} before churn");
            }

            for(int i = this.RefreshWorkingSetSize; i<this.RefreshDataSize; i++) {
                owner[this.MakeKey(i)]=this.MakeSegmentCreatedValue(i);
                Assert.True(owner.Remove(this.MakeKey(i-this.RefreshWorkingSetSize)),
                    $"Expected key {this.MakeKey(i-this.RefreshWorkingSetSize)} to exist before removal during churn");
            }

            var liveKey = this.MakeKey(this.RefreshDataSize-1);

            owner.Refresh();
            sibling1.Refresh();
            Assert.True(owner.TryGetValue(liveKey,out _),$"Expected owner to observe live key {liveKey} after partial refresh");
            Assert.True(sibling1.TryGetValue(liveKey,out _),$"Expected sibling1 to observe live key {liveKey} after partial refresh");

            handle.Flush();
            handle.Compact();
            handle.FlushAndEvict();

            var beginAddressWithStaleSibling = handle.GetBeginAddress();
            var segmentFilesWithStaleSibling = handle.CountSegmentFiles();
            this.output.WriteLine($"[with_stale_sibling] BeginAddress after compact: {beginAddressWithStaleSibling}, Segment files: {segmentFilesWithStaleSibling}");

            sibling2.Refresh();
            Assert.True(sibling2.TryGetValue(liveKey,out _),$"Expected sibling2 to observe live key {liveKey} after final refresh");

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
    public void 複数DictionaryでRefresh後も共有ストアのデータを継続参照できる() {
        var storeDir = this.TestDir(nameof(this.複数DictionaryでRefresh後も共有ストアのデータを継続参照できる));
        using var handle = this.CreateHandle(storeDir);
        using var owner = handle.Create();
        using var sibling1 = handle.Create();
        using var sibling2 = handle.Create();

        var count = Math.Min(32,this.RefreshWorkingSetSize);
        for(int i = 0; i<count; i++) {
            owner.Add(this.MakeKey(i),this.MakeValue(i));
        }

        for(int i = 0; i<count; i++) {
            Assert.True(sibling1.TryGetValue(this.MakeKey(i),out var value1),$"Expected sibling1 to observe key {this.MakeKey(i)} before refresh");
            Assert.Equal(this.MakeValue(i),value1!);
            Assert.True(sibling2.TryGetValue(this.MakeKey(i),out var value2),$"Expected sibling2 to observe key {this.MakeKey(i)} before refresh");
            Assert.Equal(this.MakeValue(i),value2!);
        }

        owner.Refresh();
        sibling1.Refresh();
        sibling2.Refresh();

        for(int i = 0; i<count; i++) {
            Assert.True(owner.TryGetValue(this.MakeKey(i),out var ownerValue),$"Expected owner to observe key {this.MakeKey(i)} after refresh");
            Assert.Equal(this.MakeValue(i),ownerValue!);
            Assert.True(sibling1.TryGetValue(this.MakeKey(i),out var value1),$"Expected sibling1 to observe key {this.MakeKey(i)} after refresh");
            Assert.Equal(this.MakeValue(i),value1!);
            Assert.True(sibling2.TryGetValue(this.MakeKey(i),out var value2),$"Expected sibling2 to observe key {this.MakeKey(i)} after refresh");
            Assert.Equal(this.MakeValue(i),value2!);
        }

        var extraIndex = count+1;
        owner[this.MakeKey(extraIndex)]=this.MakeValue(extraIndex);
        Assert.True(sibling1.TryGetValue(this.MakeKey(extraIndex),out var refreshedValue1),$"Expected sibling1 to observe key {this.MakeKey(extraIndex)} after refresh");
        Assert.Equal(this.MakeValue(extraIndex),refreshedValue1!);
        Assert.True(sibling2.TryGetValue(this.MakeKey(extraIndex),out var refreshedValue2),$"Expected sibling2 to observe key {this.MakeKey(extraIndex)} after refresh");
        Assert.Equal(this.MakeValue(extraIndex),refreshedValue2!);
        Assert.True(owner.Remove(this.MakeKey(extraIndex)),$"Expected remove for key {this.MakeKey(extraIndex)} after refresh to succeed");
    }

    [Fact]
    public void チェックポイントと復元() {
        var storeDir = this.TestDir(nameof(this.チェックポイントと復元));
        var checkpointDir = Path.Combine(storeDir,"checkpoint");
        Directory.CreateDirectory(storeDir);
        Directory.CreateDirectory(checkpointDir);

        var dataSize = this.CheckpointDataSize;
        Guid token = default;

        // データを書き込んでチェックポイントを保存
        {
            var handle = this.CreateHandle(storeDir,checkpointDir,deleteOnDispose: false);
            try {
                using var dict = handle.Create();
                for(int i = 0; i<dataSize; i++)
                    dict.Add(this.MakeKey(i),this.MakeValue(i));
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
                using var dict = handle.Create();
                for(int i = 0; i<dataSize; i++) {
                    Assert.True(dict.TryGetValue(this.MakeKey(i),out var val),$"Key {this.MakeKey(i)} not found after restore");
                    Assert.Equal(this.MakeValue(i),val!);
                }
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
/// ManagementDictionary: Seamless Dictionary (Managed Key × Managed Value) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class ManagementDictionary(ITestOutputHelper output):DictionaryIntegrateBase<string,string>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.ManagementDictionary";
    protected override string MakeKey(int i) => $"key_{i:D6}";
    protected override string MakeValue(int i) => $"value_{i:D6}_"+new string('x',100);
    protected override string MakeSegmentCreatedValue(int i) => $"value_{i:D6}_"+new string('x',2048);
    protected override int SegmentsCreatedMaxInserts => 200;
    protected override int SegmentsCreatedCheckInterval => 10;
    protected override bool SegmentsCreatedBreakOnFirstGrowth => true;
    protected override int RefreshDataSize => 1024;
    protected override int RefreshWorkingSetSize => 64;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir = null,bool deleteOnDispose = true) =>
        new ManagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class ManagedHandle:Handle {
        private readonly ManagedManagedDictionaryStore<string,string> store;

        public ManagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory = HashStore.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<string,string>(this.logDevice,this.objDevice,new ManagedTag(),new ManagedTag());
        }

        public override ISeamlessDictionary<string,string> Create() =>
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
/// UnmanagementDictionary: Seamless Dictionary (Unmanaged Key × Unmanaged Value) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class UnmanagementDictionary(ITestOutputHelper output):DictionaryIntegrateBase<int,int>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.UnmanagementDictionary";
    protected override int MakeKey(int i) => i;
    protected override int MakeValue(int i) => i*100;
    protected override int CheckpointDataSize => 500;
    protected override int RefreshDataSize => 4096;
    protected override int RefreshWorkingSetSize => 128;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir = null,bool deleteOnDispose = true) =>
        new UnmanagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class UnmanagedHandle:Handle {
        private readonly UnmanagedUnmanagedDictionaryStore<int,int> store;

        public UnmanagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory = HashStore.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<int,int>(this.logDevice,this.objDevice,new UnmanagedTag(),new UnmanagedTag());
        }

        public override ISeamlessDictionary<int,int> Create() =>
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
