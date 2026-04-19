using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Integrate;

/// <summary>
/// HashSet (Key) のセグメント管理・チェックポイントテストの基底クラス。
/// MutableFractionを最小化（0.05）、SegmentSizeも最小化（PageSize）して、
/// セグメントファイルがどんどん作成される様子をテストする。
/// </summary>
public abstract class HashSetIntegrateBase<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly ITestOutputHelper output;

    protected HashSetIntegrateBase(ITestOutputHelper output) {
        this.output=output;
    }

    protected abstract string TestClassName { get; }
    protected abstract TKey MakeKey(int i);
    protected abstract Handle CreateHandle(string rootDir,string? checkpointDir=null,bool deleteOnDispose=true);
    protected virtual int SegmentsCreatedDataSize => 5000;
    protected virtual int SegmentsCreatedCheckInterval => 500;
    protected virtual int SegmentGrowthDataSize => 3000;
    protected virtual int SegmentGrowthCheckInterval => 300;
    protected virtual int CheckpointDataSize => 200;

    private string TestDir(string methodName) =>
        Path.Combine(AppContext.BaseDirectory,TestClassName,methodName,Guid.NewGuid().ToString("N"));

    protected abstract class Handle : IDisposable {
        protected readonly string rootDir;
        protected readonly IDevice logDevice;
        protected readonly IDevice objDevice;
        private readonly bool deleteOnDispose;

        protected Handle(string rootDir,bool deleteOnDispose=true) {
            this.rootDir=rootDir;
            this.deleteOnDispose=deleteOnDispose;
            Directory.CreateDirectory(rootDir);
            this.logDevice=Devices.CreateLogDevice(Path.Combine(rootDir,"log"),deleteOnClose: false);
            this.objDevice=Devices.CreateLogDevice(Path.Combine(rootDir,"obj"),deleteOnClose: false);
        }

        public int CountSegmentFiles() => Directory.GetFiles(rootDir,"log.*").Length;
        public abstract ISeamlessHashSet<TKey> Create();
        public abstract Guid Save();
        public abstract void Load(Guid token);
        protected abstract void DisposeStore();

        public void Dispose() {
            DisposeStore();
            logDevice.Dispose();
            objDevice.Dispose();
            if(deleteOnDispose) try { Directory.Delete(rootDir,true); } catch { }
        }
    }

    [Fact]
    public void データ追加中にセグメントファイルが増加することを確認する() {
        var storeDir=TestDir(nameof(データ追加中にセグメントファイルが増加することを確認する));
        using var handle=CreateHandle(storeDir);
        using var hashset=handle.Create();

        var initialSegmentCount=handle.CountSegmentFiles();
        output.WriteLine($"[Initial] Segment files: {initialSegmentCount}");

        try {
            for(int i=0; i<SegmentsCreatedDataSize; i++) {
                hashset.Add(MakeKey(i));

                if((i+1)%SegmentsCreatedCheckInterval==0) {
                    var segmentCount=handle.CountSegmentFiles();
                    output.WriteLine($"[After {i+1} adds] Segment files: {segmentCount}");
                }
            }

            var finalSegmentCount=handle.CountSegmentFiles();
            output.WriteLine($"[Final] Segment files: {finalSegmentCount}");
            output.WriteLine($"✓ Segments created: {finalSegmentCount} files");

            Assert.True(finalSegmentCount>initialSegmentCount,
                $"Expected more segments than initial ({initialSegmentCount}), but got {finalSegmentCount}");
        }
        finally { }
    }

    [Fact]
    public void セグメント数がデータ追加に伴い単調増加することを確認する() {
        var storeDir=TestDir(nameof(セグメント数がデータ追加に伴い単調増加することを確認する));
        using var handle=CreateHandle(storeDir);
        using var hashset=handle.Create();

        var segmentCounts=new List<int>();
        var addCountAtSnapshots=new List<int>();

        try {
            for(int i=0; i<SegmentGrowthDataSize; i++) {
                hashset.Add(MakeKey(i));

                if((i+1)%SegmentGrowthCheckInterval==0) {
                    var count=handle.CountSegmentFiles();
                    segmentCounts.Add(count);
                    addCountAtSnapshots.Add(i+1);
                    output.WriteLine($"[{i+1} adds] Segments: {count}");
                }
            }

            for(int i=1; i<segmentCounts.Count; i++) {
                Assert.True(segmentCounts[i]>=segmentCounts[i-1],
                    $"Segment count decreased at iteration {i}: {segmentCounts[i-1]} -> {segmentCounts[i]}");
            }

            output.WriteLine($"✓ Segment growth is progressive: {string.Join(" -> ",segmentCounts)} files");
        }
        finally { }
    }

    [Fact]
    public void チェックポイントと復元() {
        var storeDir=TestDir(nameof(チェックポイントと復元));
        var checkpointDir=Path.Combine(storeDir,"checkpoint");
        Directory.CreateDirectory(storeDir);
        Directory.CreateDirectory(checkpointDir);

        var dataSize=CheckpointDataSize;
        Guid token=default;

        // データを書き込んでチェックポイントを保存
        {
            var handle=CreateHandle(storeDir,checkpointDir,deleteOnDispose: false);
            try {
                using var hashset=handle.Create();
                for(int i=0; i<dataSize; i++)
                    hashset.Add(MakeKey(i));
                token=handle.Save();
                output.WriteLine($"✓ Checkpoint saved: {token}");
            }
            finally {
                handle.Dispose();
            }
        }

        // チェックポイントから復元してデータを検証
        {
            var handle=CreateHandle(storeDir,checkpointDir,deleteOnDispose: false);
            try {
                handle.Load(token);
                using var hashset=handle.Create();
                for(int i=0; i<dataSize; i++)
                    Assert.True(hashset.Contains(MakeKey(i)),$"Key {MakeKey(i)} not found after restore");
                output.WriteLine($"✓ All {dataSize} entries verified after restore");
            }
            finally {
                handle.Dispose();
            }
        }
        try { Directory.Delete(storeDir,true); } catch { }
    }
}

/// <summary>
/// ManagementHashSet: Seamless HashSet (Managed Key) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class ManagementHashSet(ITestOutputHelper output) : HashSetIntegrateBase<string>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.ManagementHashSet";
    protected override string MakeKey(int i) => $"hashset_key_{i:D6}_"+new string('a',50);
    protected override int SegmentsCreatedDataSize => 10000;
    protected override int SegmentsCreatedCheckInterval => 1000;
    protected override int SegmentGrowthDataSize => 5000;
    protected override int SegmentGrowthCheckInterval => 500;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir=null,bool deleteOnDispose=true) =>
        new ManagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class ManagedHandle : Handle {
        private readonly ManagedUnmanagedHashSetStore<string> store;

        public ManagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory=Factory.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<string>(logDevice,objDevice,new ManagedTag());
        }

        public override ISeamlessHashSet<string> Create() => store.Create();
        public override Guid Save() => store.Save();
        public override void Load(Guid token) => store.Load(token);

        protected override void DisposeStore() {
            store.Store.Log.FlushAndEvict(wait: true);
            store.Dispose();
        }
    }
}

/// <summary>
/// UnmanagementHashSet: Seamless HashSet (Unmanaged Key) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class UnmanagementHashSet(ITestOutputHelper output) : HashSetIntegrateBase<int>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.UnmanagementHashSet";
    protected override int MakeKey(int i) => i;
    protected override int CheckpointDataSize => 500;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir=null,bool deleteOnDispose=true) =>
        new UnmanagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class UnmanagedHandle : Handle {
        private readonly UnmanagedUnmanagedHashSetStore<int> store;

        public UnmanagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory=Factory.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<int>(logDevice,objDevice,new UnmanagedTag());
        }

        public override ISeamlessHashSet<int> Create() => store.Create();
        public override Guid Save() => store.Save();
        public override void Load(Guid token) => store.Load(token);

        protected override void DisposeStore() {
            store.Store.Log.FlushAndEvict(wait: true);
            store.Dispose();
        }
    }
}
