using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Stores;
using Tsavorite.core;

namespace BenchmarkExe.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class DictionaryStoreBenchmark {
    private int[] keys = null!;
    private string checkpointRoot = null!;
    private string? loadCheckpointDir;
    private Guid loadToken;

    [Params(10_000,100_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.keys=new int[this.Count];
        for(var i = 0; i<this.keys.Length; i++)
            this.keys[i]=i;

        this.checkpointRoot=BenchmarkOutputLayout.CreateCategoryWorkspaceRoot("Dictionary");
        Directory.CreateDirectory(this.checkpointRoot);
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        if(Directory.Exists(this.checkpointRoot))
            Directory.Delete(this.checkpointRoot,recursive: true);
    }

    [Benchmark(Description = "Dictionary Add")]
    public long Add() {
        using var store = this.CreateStore();
        using var dict = store.Create();

        for(var i = 0; i<this.keys.Length; i++)
            dict.Add(this.keys[i],this.keys[i]);

        return dict.GetLongCount();
    }

    [Benchmark(Description = "Dictionary Remove")]
    public long Remove() {
        using var store = this.CreateStore();
        using var dict = store.Create();

        for(var i = 0; i<this.keys.Length; i++)
            dict.Add(this.keys[i],this.keys[i]);

        for(var i = 0; i<this.keys.Length; i++)
            dict.Remove(this.keys[i]);

        return dict.GetLongCount();
    }

    [WarmupCount(1)]
    [IterationCount(1)]
    [InvocationCount(1)]
    [Benchmark(Description = "Store Save")]
    public Guid Save() {
        var checkpointDir = Path.Combine(this.checkpointRoot,"save-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(checkpointDir);

        var logPath = Path.Combine(checkpointDir,"log");
        var objPath = Path.Combine(checkpointDir,"obj");
        using var logDevice = Devices.CreateLogDevice(logPath);
        using var objDevice = Devices.CreateLogDevice(objPath);
        using var store = this.CreateStore(checkpointDir,logDevice,objDevice);
        using var dict = store.Create();

        for(var i = 0; i<this.keys.Length; i++)
            dict.Add(this.keys[i],this.keys[i]);

        return store.Save();
    }

    [IterationSetup(Target = nameof(Load))]
    public void PrepareLoadCheckpoint() {
        this.loadCheckpointDir=Path.Combine(this.checkpointRoot,"load-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.loadCheckpointDir);

        var logPath = Path.Combine(this.loadCheckpointDir,"log");
        var objPath = Path.Combine(this.loadCheckpointDir,"obj");
        using var logDevice = Devices.CreateLogDevice(logPath);
        using var objDevice = Devices.CreateLogDevice(objPath);
        using var store = this.CreateStore(this.loadCheckpointDir,logDevice,objDevice);
        using var dict = store.Create();

        for(var i = 0; i<this.keys.Length; i++)
            dict.Add(this.keys[i],this.keys[i]);

        this.loadToken=store.Save();
    }

    [IterationCleanup(Target = nameof(Load))]
    public void CleanupLoadCheckpoint() {
        if(this.loadCheckpointDir is null)
            return;

        if(Directory.Exists(this.loadCheckpointDir))
            Directory.Delete(this.loadCheckpointDir,recursive: true);

        this.loadCheckpointDir=null;
        this.loadToken=Guid.Empty;
    }

    [WarmupCount(1)]
    [IterationCount(1)]
    [InvocationCount(1)]
    [Benchmark(Description = "Store Load")]
    public long Load() {
        if(this.loadCheckpointDir is null)
            throw new InvalidOperationException("Load benchmark checkpoint is not prepared.");

        var logPath = Path.Combine(this.loadCheckpointDir,"log");
        var objPath = Path.Combine(this.loadCheckpointDir,"obj");
        using var logDevice = Devices.CreateLogDevice(logPath);
        using var objDevice = Devices.CreateLogDevice(objPath);
        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.Count);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.Count*128L);
        var factory = HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: 1.0,CheckpointDir: this.loadCheckpointDir)
            .WithRecoveryIndexSize(this.loadToken);
        using var store = factory.Create<int,int>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
        store.Load(this.loadToken);
        using var dict = store.Create();
        return dict.GetLongCount();
    }

    private DictionaryStore<int,int> CreateStore(string? checkpointDir = null) {
        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.Count);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.Count*128L);
        return HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: 1.0,CheckpointDir: checkpointDir).Create<int,int>(new BlittableStrategy(),new BlittableStrategy());
    }

    private DictionaryStore<int,int> CreateStore(string checkpointDir,IDevice logDevice,IDevice objDevice) {
        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.Count);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.Count*128L);
        return HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: 1.0,CheckpointDir: checkpointDir).Create<int,int>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
    }
}
