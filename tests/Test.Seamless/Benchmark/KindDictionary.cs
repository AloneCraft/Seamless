using System.Diagnostics;
using Seamless.Collections;
using Seamless.Stores;
namespace Benchmarks;

/// <summary>
/// 4種類のDictionary(ManagedManaged, UnmanagedManaged, ManagedUnmanaged, UnmanagedUnmanaged)の
/// パフォーマンスとログサイズを計測するベンチマーク。
/// </summary>
[Collection("Benchmark")]
public class KindDictionary(ITestOutputHelper output) {
    private const int Count = 100_000;
    private const long MemorySize = Seamless.Const.LogMemorySize << 12;

    private void RunBenchmark(
        string label,
        Func<long> getTailAddress,
        Func<long> getBeginAddress,
        ISeamlessDictionary<int,int> dict) {
        // Add
        var sw = Stopwatch.StartNew();
        for(var i = 0; i < Count; i++) dict.Add(i, i);
        sw.Stop();
        var addMs = sw.Elapsed.TotalMilliseconds;
        var logSize = getTailAddress() - getBeginAddress();
        var bytesPerEntry = logSize / (double)Count;

        // ContainsKey
        sw.Restart();
        for(var i = 0; i < Count; i++) _ = dict.ContainsKey(i);
        sw.Stop();
        var containsMs = sw.Elapsed.TotalMilliseconds;

        // TryGetValue
        sw.Restart();
        for(var i = 0; i < Count; i++) _ = dict.TryGetValue(i, out _);
        sw.Stop();
        var getMs = sw.Elapsed.TotalMilliseconds;

        output.WriteLine(
            $"[{label}] Add={addMs:F0}ms ({addMs*1000/Count:F1}us/op) | " +
            $"ContainsKey={containsMs:F0}ms ({containsMs*1000/Count:F1}us/op) | " +
            $"TryGetValue={getMs:F0}ms ({getMs*1000/Count:F1}us/op) | " +
            $"LogSize={logSize/1024:N0}KB ({bytesPerEntry:F1}bytes/entry)");
    }

    [Fact, Trait("Category","")]
    public void _UnmanagedUnmanaged_int_int() {
        var IndexSize = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Count);
        using var store = Factory.Create(IndexSize:IndexSize, MemorySize:MemorySize).Create<int,int>();
        using var dict  = store.Create();
        this.RunBenchmark("UnmanagedUnmanaged<int,int>", ()=>store.Store.Log.TailAddress, ()=>store.Store.Log.BeginAddress, dict);
    }

    [Fact, Trait("Category","")]
    public void _UnmanagedManaged_int_int() {
        var IndexSize = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Count);
        using var store = Factory.Create(IndexSize:IndexSize, MemorySize:MemorySize).Create<int,int>(default, new ManagedTag());
        using var dict  = store.Create();
        this.RunBenchmark("UnmanagedManaged<int,int>", ()=>store.Store.Log.TailAddress, ()=>store.Store.Log.BeginAddress, dict);
    }

    [Fact, Trait("Category","")]
    public void _ManagedUnmanaged_int_int() {
        var IndexSize = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Count);
        using var store = Factory.Create(IndexSize:IndexSize, MemorySize:MemorySize).Create<int,int>(new ManagedTag(), default);
        using var dict  = store.Create();
        this.RunBenchmark("ManagedUnmanaged<int,int>", ()=>store.Store.Log.TailAddress, ()=>store.Store.Log.BeginAddress, dict);
    }

    [Fact, Trait("Category","")]
    public void _ManagedManaged_int_int() {
        var IndexSize = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Count);
        using var store = Factory.Create(IndexSize:IndexSize, MemorySize:MemorySize).Create<int,int>(new ManagedTag(), new ManagedTag());
        using var dict  = store.Create();
        this.RunBenchmark("ManagedManaged<int,int>", ()=>store.Store.Log.TailAddress, ()=>store.Store.Log.BeginAddress, dict);
    }
}
