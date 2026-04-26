using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
using System.IO.MemoryMappedFiles;

namespace BenchmarkExe.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class Benchmark {
    private int[] keys = null!;
    private string listRoot = null!;

    [Params(10_000,100_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.keys=new int[this.Count];
        for(var i = 0; i<this.keys.Length; i++)
            this.keys[i]=i;

        this.listRoot=Path.Combine(Environment.CurrentDirectory,"Seamless-Benchmark-List",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.listRoot);
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        if(Directory.Exists(this.listRoot))
            Directory.Delete(this.listRoot,recursive: true);
    }

    [Benchmark(Description = "Dictionary_Add")]
    public long Dictionary_Add() {
        using var store = this.CreateStore();
        using var dict = store.Create();

        for(var i = 0; i<this.keys.Length; i++)
            dict.Add(this.keys[i],this.keys[i]);

        return dict.GetLongCount();
    }

    [Benchmark(Description = "Dictionary_Remove")]
    public long Dictionary_Remove() {
        using var store = this.CreateStore();
        using var dict = store.Create();

        for(var i = 0; i<this.keys.Length; i++)
            dict.Add(this.keys[i],this.keys[i]);

        for(var i = 0; i<this.keys.Length; i++)
            dict.Remove(this.keys[i]);

        return dict.GetLongCount();
    }

    [Benchmark(Description = "List_Add")]
    public long List_Add() {
        using var list = this.CreateList(Path.Combine(this.listRoot,"list-add-"+Guid.NewGuid().ToString("N")+".data"));
        for(var i = 0; i<this.keys.Length; i++)
            list.Add(this.keys[i]);
        return list.Count;
    }

    [Benchmark(Description = "List_Remove")]
    public long List_Remove() {
        using var list = this.CreateList(Path.Combine(this.listRoot,"list-remove-"+Guid.NewGuid().ToString("N")+".data"));
        for(var i = 0; i<this.keys.Length; i++)
            list.Add(this.keys[i]);
        for(var i = 0; i<this.keys.Length; i++)
            list.Remove();
        return list.Count;
    }

    private UnmanagedDictionaryStore<int,int> CreateStore() {
        var indexSize = Math.Max(2048,this.Count*2);
        var memorySize = Math.Max(4L*1024*1024,this.Count*128L);
        return HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: 1.0).Create<int,int>();
    }

    private UnmanagedList<int> CreateList(string listPath) {
        var list = ListStore.Create<int>(listPath,initialCapacity: Math.Max(16,this.Count),new UnmanagedTag());
        list.SetTuning(new TempListTuning(FileAccess.ReadWrite,FileOptions.SequentialScan));
        return list;
    }
}
