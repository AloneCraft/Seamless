using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Seamless.Stores;

namespace BenchmarkExe.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public sealed class DictionaryStoreBenchmark {
    private int[] keys = null!;
    private string checkpointRoot = null!;
    private string? loadCheckpointDir;
    private Guid loadToken;

    [Params(10_000, 100_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.keys = new int[this.Count];
        for (var i = 0; i < this.keys.Length; i++)
            this.keys[i] = i;

        this.checkpointRoot = Path.Combine(Path.GetTempPath(), "Seamless-Benchmark-Checkpoints", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.checkpointRoot);
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        if (Directory.Exists(this.checkpointRoot))
            Directory.Delete(this.checkpointRoot, recursive: true);
    }

    [Benchmark(Description = "Dictionary Add")]
    public long Add() {
        using var store = this.CreateStore();
        using var dict = store.Create();

        for (var i = 0; i < this.keys.Length; i++)
            dict.Add(this.keys[i], this.keys[i]);

        return dict.GetLongCount();
    }

    [Benchmark(Description = "Dictionary Remove")]
    public long Remove() {
        using var store = this.CreateStore();
        using var dict = store.Create();

        for (var i = 0; i < this.keys.Length; i++)
            dict.Add(this.keys[i], this.keys[i]);

        for (var i = 0; i < this.keys.Length; i++)
            dict.Remove(this.keys[i]);

        return dict.GetLongCount();
    }

    [Benchmark(Description = "Store Save")]
    public Guid Save() {
        var checkpointDir = Path.Combine(this.checkpointRoot, "save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(checkpointDir);

        using var store = this.CreateStore(checkpointDir);
        using var dict = store.Create();

        for (var i = 0; i < this.keys.Length; i++)
            dict.Add(this.keys[i], this.keys[i]);

        return store.Save();
    }

    [IterationSetup(Target = nameof(Load))]
    public void PrepareLoadCheckpoint() {
        this.loadCheckpointDir = Path.Combine(this.checkpointRoot, "load-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.loadCheckpointDir);

        using var store = this.CreateStore(this.loadCheckpointDir);
        using var dict = store.Create();

        for (var i = 0; i < this.keys.Length; i++)
            dict.Add(this.keys[i], this.keys[i]);

        this.loadToken = store.Save();
    }

    [IterationCleanup(Target = nameof(Load))]
    public void CleanupLoadCheckpoint() {
        if (this.loadCheckpointDir is null)
            return;

        if (Directory.Exists(this.loadCheckpointDir))
            Directory.Delete(this.loadCheckpointDir, recursive: true);

        this.loadCheckpointDir = null;
        this.loadToken = Guid.Empty;
    }

    [Benchmark(Description = "Store Load")]
    public long Load() {
        if (this.loadCheckpointDir is null)
            throw new InvalidOperationException("Load benchmark checkpoint is not prepared.");

        using var store = this.CreateStore(this.loadCheckpointDir);
        store.Load(this.loadToken);
        using var dict = store.Create();
        return dict.GetLongCount();
    }

    private UnmanagedUnmanagedDictionaryStore<int, int> CreateStore(string? checkpointDir = null) {
        var indexSize = Math.Max(2048, this.Count * 2);
        var memorySize = Math.Max(4L * 1024 * 1024, this.Count * 128L);
        return Factory.Create(IndexSize: indexSize, MemorySize: memorySize, MutableFraction: 1.0, CheckpointDir: checkpointDir).Create<int, int>();
    }
}
