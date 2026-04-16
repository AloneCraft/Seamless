using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Benchmarks;

[Collection("Benchmark")]
public class CheckpointIndexResizeBenchmark(ITestOutputHelper output)
{
    private const int DataCount = 256;
    private const long TinyIndexSize = 1L << 6;   // 64
    private const long LargeIndexSize = 1L << 10; // 1024
    private const long TinyMemorySize = 1L << 13; // 8192 (default PageSize=4096 の最小構成)
    private const double MutableFraction = 0.0;

    private const string TempRoot = @"G:\temp";

    [Fact]
    [Trait("Category", "Benchmark")]
    public void チェックポイント1_Full_セーブロードを繰り返しロードできる()
    {
        var prefix = CreateUniquePrefix();
        var token = SaveFullCheckpoint(prefix, TinyIndexSize);

        for (var i = 0; i < 5; i++)
        {
            using var store = CreateStore(prefix, TinyIndexSize);
            store.Load(token);
            using var dict = store.Create();
            AssertData(dict);
        }

        output.WriteLine("Full checkpoint repeated load completed.");
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void チェックポイント2_LogOnly_セーブロードを繰り返しロードできる()
    {
        var prefix = CreateUniquePrefix();
        var token = SaveLogOnlyCheckpoint(prefix, TinyIndexSize);

        for (var i = 0; i < 5; i++)
        {
            using var store = CreateStore(prefix, TinyIndexSize);
            store.Load(token);
            using var dict = store.Create();
            AssertData(dict);
        }

        output.WriteLine("Log-only checkpoint repeated load completed.");
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void インデックス無しデータログのみ復旧_現在データを維持して拡大縮小できる()
    {
        var prefix = CreateUniquePrefix();
        var token = SaveLogOnlyCheckpoint(prefix, TinyIndexSize);

        using (var growStore = CreateStore(prefix, LargeIndexSize))
        {
            growStore.Load(token);
            using var growDict = growStore.Create();
            AssertData(growDict);
        }

        using (var shrinkStore = CreateStore(prefix, TinyIndexSize))
        {
            shrinkStore.Load(token);
            using var shrinkDict = shrinkStore.Create();
            AssertData(shrinkDict);
        }

        output.WriteLine("Log-only recovery with index grow/shrink completed.");
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void チェックポイント1方式_Saveしてインデックス拡大ロード後に縮小ロードできる()
    {
        var prefix = CreateUniquePrefix();
        var token = SaveFullCheckpoint(prefix, TinyIndexSize);

        using (var growStore = CreateStore(prefix, LargeIndexSize))
        {
            growStore.Load(token);
            using var growDict = growStore.Create();
            AssertData(growDict);
        }

        using (var shrinkStore = CreateStore(prefix, TinyIndexSize))
        {
            shrinkStore.Load(token);
            using var shrinkDict = shrinkStore.Create();
            AssertData(shrinkDict);
        }

        output.WriteLine("Full checkpoint recovery with index grow/shrink completed.");
    }

    private static string CreateUniquePrefix()
    {
        Directory.CreateDirectory(TempRoot);
        return Path.Combine(TempRoot, "SeamlessCheckpointBenchmark_" + Guid.NewGuid().ToString("N"));
    }

    private static UnmanagedUnmanagedDictionaryStore<int, int> CreateStore(string prefix, long indexSize)
    {
        var checkpointDir = prefix + "_checkpoint";
        var logDevice = Devices.CreateLogDevice(prefix);
        var objDevice = Devices.CreateLogDevice(prefix + "_obj");
        return Factory.Create(
            IndexSize: indexSize,
            MemorySize: TinyMemorySize,
            MutableFraction: MutableFraction,
            CheckpointDir: checkpointDir).Create<int, int>(logDevice, objDevice);
    }

    private static Guid SaveFullCheckpoint(string prefix, long indexSize)
    {
        using var store = CreateStore(prefix, indexSize);
        using var dict = store.Create();
        FillData(dict);
        AssertData(dict);
        return store.Save();
    }

    private static Guid SaveLogOnlyCheckpoint(string prefix, long indexSize)
    {
        using var store = CreateStore(prefix, indexSize);
        using var dict = store.Create();
        FillData(dict);
        AssertData(dict);
        return store.SaveLogOnly();
    }

    private static void FillData(ISeamlessDictionary<int, int> dict)
    {
        for (var i = 0; i < DataCount; i++)
        {
            dict[i] = i * 10;
        }
    }

    private static void AssertData(ISeamlessDictionary<int, int> dict)
    {
        for (var i = 0; i < DataCount; i++)
        {
            Assert.True(dict.TryGetValue(i, out var value), $"Key {i} was not recovered.");
            Assert.Equal(i * 10, value);
        }

        Assert.False(dict.ContainsKey(DataCount + 1));
    }

}
