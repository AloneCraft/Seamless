using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Collections;
using Tsavorite.Stores;

namespace BenchmarkExe.Benchmarks;

public enum ListOperation {
    Add,
    Read,
    Mixed,
}

public enum ListAccessPattern {
    SequentialScan,
    RandomAccess,
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class ListApiSurfaceBenchmark {
    private string iterationDir = null!;
    private global::Tsavorite.Collections.ListBase<int>? list;

    [Params(10_000)]
    public int CountValue { get; set; }

    [Params(CollectionImplKind.Managed,CollectionImplKind.Blittable)]
    public CollectionImplKind Impl { get; set; }

    [Params(ListAccessPattern.SequentialScan,ListAccessPattern.RandomAccess)]
    public ListAccessPattern AccessPattern { get; set; }

    [Params(ListOperation.Add,ListOperation.Read,ListOperation.Mixed)]
    public ListOperation Operation { get; set; }

    [IterationSetup]
    public void IterationSetup() {
        this.DisposeIterationResources();

        this.iterationDir=BenchmarkOutputLayout.CreateCategoryWorkspaceRoot("List");
        Directory.CreateDirectory(this.iterationDir);

        var listPath = Path.Combine(this.iterationDir,"list.data");
        this.list=this.Impl==CollectionImplKind.Managed
            ? ListStore.Create<int>(listPath,initialCapacity: 16,initialDataCapacity: Math.Max(4096,this.CountValue*8),new MemoryPackSerializerStrategy())
            : ListStore.Create<int>(listPath,initialCapacity: Math.Max(16,this.CountValue),new BlittableStrategy());

        this.list.SetTuning(new TempListTuning(FileAccess.ReadWrite,this.ToFileOptions(this.AccessPattern)));

        if(this.Operation==ListOperation.Read) {
            for(var i = 0; i<this.CountValue; i++) {
                this.list.Add(i);
            }
        }
    }

    [IterationCleanup]
    public void IterationCleanup() {
        this.DisposeIterationResources();
    }

    [Benchmark]
    public long Run() {
        var target = this.list??throw new InvalidOperationException("Iteration setup was not completed.");

        return this.Operation switch {
            ListOperation.Add => this.RunAdd(target),
            ListOperation.Read => this.RunRead(target),
            ListOperation.Mixed => this.RunMixed(target),
            _ => 0,
        };
    }

    private long RunAdd(global::Tsavorite.Collections.ListBase<int> target) {
        for(var i = 0; i<this.CountValue; i++) {
            target.Add(i);
        }
        return target.Count;
    }

    private long RunRead(global::Tsavorite.Collections.ListBase<int> target) {
        long sum = 0;
        for(var i = 0; i<this.CountValue; i++) {
            sum+=target[i];
        }
        return sum;
    }

    private long RunMixed(global::Tsavorite.Collections.ListBase<int> target) {
        var n = this.CountValue/2;
        for(var i = 0; i<n; i++) {
            target.Add(i);
        }

        long sum = 0;
        for(var i = 0; i<n; i++) {
            sum+=target[i];
        }
        return sum;
    }

    private FileOptions ToFileOptions(ListAccessPattern pattern) {
        return pattern==ListAccessPattern.SequentialScan
            ? FileOptions.SequentialScan
            : FileOptions.RandomAccess;
    }

    private void DisposeIterationResources() {
        this.list?.Dispose();
        this.list=null;

        if(!string.IsNullOrEmpty(this.iterationDir)&&Directory.Exists(this.iterationDir)) {
            Directory.Delete(this.iterationDir,recursive: true);
        }
    }
}
