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
    where TValue : IEquatable<TValue>
{
    private readonly ITestOutputHelper output;

    protected DictionaryIntegrateBase(ITestOutputHelper output) {
        this.output=output;
    }

    protected abstract string TestClassName { get; }
    protected abstract TKey MakeKey(int i);
    protected abstract TValue MakeValue(int i);
    protected virtual TValue MakeSegmentCreatedValue(int i) => MakeValue(i);
    protected abstract Handle CreateHandle(string rootDir,string? checkpointDir=null,bool deleteOnDispose=true);
    protected virtual int SegmentsCreatedMaxInserts => 5000;
    protected virtual int SegmentsCreatedCheckInterval => 500;
    protected virtual bool SegmentsCreatedBreakOnFirstGrowth => false;
    protected virtual int SegmentGrowthDataSize => 5000;
    protected virtual int SegmentGrowthCheckInterval => 500;
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
        /// <summary>SegmentsCreatedDuringDataInsertion でのチェック前フラッシュ。既定は no-op。</summary>
        public virtual void Flush() { }
        public abstract ISeamlessDictionary<TKey,TValue> Create();
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
    public void データ挿入中にセグメントファイルが増加することを確認する() {
        var storeDir=TestDir(nameof(データ挿入中にセグメントファイルが増加することを確認する));
        using var handle=CreateHandle(storeDir);
        using var dict=handle.Create();

        var initialSegmentCount=handle.CountSegmentFiles();
        output.WriteLine($"[Initial] Segment files: {initialSegmentCount}");

        var firstGrowthAt=-1;
        try {
            for(int i=0; i<SegmentsCreatedMaxInserts; i++) {
                dict.Add(MakeKey(i),MakeSegmentCreatedValue(i));

                if((i+1)%SegmentsCreatedCheckInterval==0) {
                    handle.Flush();
                    var segmentCount=handle.CountSegmentFiles();
                    output.WriteLine($"[After {i+1} inserts] Segment files: {segmentCount}");
                    if(SegmentsCreatedBreakOnFirstGrowth && segmentCount>initialSegmentCount) {
                        firstGrowthAt=i+1;
                        break;
                    }
                }
            }

            var finalSegmentCount=handle.CountSegmentFiles();
            output.WriteLine($"[Final] Segment files: {finalSegmentCount}");
            if(firstGrowthAt>=0)
                output.WriteLine($"✓ First segment growth observed at {firstGrowthAt} inserts");
            output.WriteLine($"✓ Segments created: {finalSegmentCount} files");

            Assert.True(finalSegmentCount>initialSegmentCount,
                $"Expected more segments than initial ({initialSegmentCount}), but got {finalSegmentCount} after {SegmentsCreatedMaxInserts} inserts");
        }
        finally { }
    }

    [Fact]
    public void セグメント数がデータ挿入に伴い単調増加することを確認する() {
        var storeDir=TestDir(nameof(セグメント数がデータ挿入に伴い単調増加することを確認する));
        using var handle=CreateHandle(storeDir);
        using var dict=handle.Create();

        var segmentCounts=new List<int>();
        var insertCountAtSnapshots=new List<int>();

        try {
            for(int i=0; i<SegmentGrowthDataSize; i++) {
                dict.Add(MakeKey(i),MakeValue(i));

                if((i+1)%SegmentGrowthCheckInterval==0) {
                    var count=handle.CountSegmentFiles();
                    segmentCounts.Add(count);
                    insertCountAtSnapshots.Add(i+1);
                    output.WriteLine($"[{i+1} inserts] Segments: {count}");
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
                using var dict=handle.Create();
                for(int i=0; i<dataSize; i++)
                    dict.Add(MakeKey(i),MakeValue(i));
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
                using var dict=handle.Create();
                for(int i=0; i<dataSize; i++) {
                    Assert.True(dict.TryGetValue(MakeKey(i),out var val),$"Key {MakeKey(i)} not found after restore");
                    Assert.Equal(MakeValue(i),val!);
                }
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
/// ManagementDictionary: Seamless Dictionary (Managed Key × Managed Value) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class ManagementDictionary(ITestOutputHelper output) : DictionaryIntegrateBase<string,string>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.ManagementDictionary";
    protected override string MakeKey(int i) => $"key_{i:D6}";
    protected override string MakeValue(int i) => $"value_{i:D6}_"+new string('x',100);
    protected override string MakeSegmentCreatedValue(int i) => $"value_{i:D6}_"+new string('x',2048);
    protected override int SegmentsCreatedMaxInserts => 200;
    protected override int SegmentsCreatedCheckInterval => 10;
    protected override bool SegmentsCreatedBreakOnFirstGrowth => true;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir=null,bool deleteOnDispose=true) =>
        new ManagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class ManagedHandle : Handle {
        private readonly ManagedManagedDictionaryStore<string,string> store;

        public ManagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory=Factory.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<string,string>(logDevice,objDevice,new ManagedTag(),new ManagedTag());
        }

        public override ISeamlessDictionary<string,string> Create() => store.Create();
        public override void Flush() => store.Store.Log.Flush(wait: true);
        public override Guid Save() => store.Save();
        public override void Load(Guid token) => store.Load(token);

        protected override void DisposeStore() {
            store.Store.Log.FlushAndEvict(wait: true);
            store.Dispose();
        }
    }
}

/// <summary>
/// UnmanagementDictionary: Seamless Dictionary (Unmanaged Key × Unmanaged Value) のセグメント管理テスト
/// </summary>
[Collection("IntegrateTests")]
public class UnmanagementDictionary(ITestOutputHelper output) : DictionaryIntegrateBase<int,int>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.UnmanagementDictionary";
    protected override int MakeKey(int i) => i;
    protected override int MakeValue(int i) => i*100;
    protected override int CheckpointDataSize => 500;

    protected override Handle CreateHandle(string rootDir,string? checkpointDir=null,bool deleteOnDispose=true) =>
        new UnmanagedHandle(rootDir,checkpointDir,deleteOnDispose);

    private sealed class UnmanagedHandle : Handle {
        private readonly UnmanagedUnmanagedDictionaryStore<int,int> store;

        public UnmanagedHandle(string rootDir,string? checkpointDir,bool deleteOnDispose) : base(rootDir,deleteOnDispose) {
            var factory=Factory.Create(
                IndexSize: 64,
                PageSize: 4096,
                SegmentSize: 4096,
                MemorySize: 16384,
                MutableFraction: 0.05,
                CheckpointDir: checkpointDir
            );
            this.store=factory.Create<int,int>(logDevice,objDevice,new UnmanagedTag(),new UnmanagedTag());
        }

        public override ISeamlessDictionary<int,int> Create() => store.Create();
        public override Guid Save() => store.Save();
        public override void Load(Guid token) => store.Load(token);

        protected override void DisposeStore() {
            store.Store.Log.FlushAndEvict(wait: true);
            store.Dispose();
        }
    }
}
