using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace BenchmarkExe.Benchmarks;

public enum HashSetOperation {
    Add,
    Remove,
    Contains,
    CopyTo,
    IEnumerableEnumerate,
    Count,
    ExceptWith,
    IntersectWith,
    SymmetricExceptWith,
    UnionWith,
    IsSubsetOf,
    IsSupersetOf,
    IsProperSubsetOf,
    IsProperSupersetOf,
    Overlaps,
    SetEquals,
    Clear,
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class HashSetApiSurfaceBenchmark {
    private int[] intKeys = null!;
    private int[] intOtherKeys = null!;
    private string[] stringKeys = null!;
    private string[] stringOtherKeys = null!;

    private IHashSet<int>? unmanagedSet;
    private IHashSet<int>? unmanagedOther;
    private IHashSet<string>? managedSet;
    private IHashSet<string>? managedOther;

    private UnmanagedHashSetStore<int>? unmanagedStore;
    private UnmanagedHashSetStore<int>? unmanagedOtherStore;
    private ManagedHashSetStore<string>? managedStore;
    private ManagedHashSetStore<string>? managedOtherStore;
    private IDevice? logA;
    private IDevice? objA;
    private IDevice? logB;
    private IDevice? objB;
    private string checkpointRoot = null!;

    [Params(10_000)]
    public int CountValue { get; set; }

    [Params(CollectionImplKind.Managed,CollectionImplKind.Unmanaged)]
    public CollectionImplKind Impl { get; set; }

    [Params(RegionKind.Mutable,RegionKind.Immutable,RegionKind.Storage)]
    public RegionKind Region { get; set; }

    [Params(
        HashSetOperation.Add,
        HashSetOperation.Remove,
        HashSetOperation.Contains,
        HashSetOperation.CopyTo,
        HashSetOperation.IEnumerableEnumerate,
        HashSetOperation.Count,
        HashSetOperation.ExceptWith,
        HashSetOperation.IntersectWith,
        HashSetOperation.SymmetricExceptWith,
        HashSetOperation.UnionWith,
        HashSetOperation.IsSubsetOf,
        HashSetOperation.IsSupersetOf,
        HashSetOperation.IsProperSubsetOf,
        HashSetOperation.IsProperSupersetOf,
        HashSetOperation.Overlaps,
        HashSetOperation.SetEquals,
        HashSetOperation.Clear)]
    public HashSetOperation Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.intKeys=new int[this.CountValue];
        this.intOtherKeys=new int[this.CountValue];
        this.stringKeys=new string[this.CountValue];
        this.stringOtherKeys=new string[this.CountValue];
        for(var i = 0; i<this.CountValue; i++) {
            this.intKeys[i]=i;
            this.intOtherKeys[i]=i+(this.CountValue/2);
            this.stringKeys[i]="s"+i;
            this.stringOtherKeys[i]="s"+(i+(this.CountValue/2));
        }

        this.checkpointRoot=BenchmarkOutputLayout.CreateCategoryWorkspaceRoot("HashSet");
        Directory.CreateDirectory(this.checkpointRoot);
    }

    [IterationSetup]
    public void IterationSetup() {
        this.DisposeIterationResources();

        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.CountValue);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.CountValue*96L);
        var mutableFraction = this.Region==RegionKind.Immutable ? 0.2 : 1.0;

        var useStorage = this.Region==RegionKind.Storage;
        if(useStorage) {
            var dir = Path.Combine(this.checkpointRoot,"iter-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            this.logA=Devices.CreateLogDevice(Path.Combine(dir,"logA"));
            this.objA=Devices.CreateLogDevice(Path.Combine(dir,"objA"));
            this.logB=Devices.CreateLogDevice(Path.Combine(dir,"logB"));
            this.objB=Devices.CreateLogDevice(Path.Combine(dir,"objB"));
        }

        var factory = HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: mutableFraction);

        if(this.Impl==CollectionImplKind.Unmanaged) {
            this.unmanagedStore=useStorage
                ? factory.Create<int>(this.logA!,this.objA!,new UnmanagedTag())
                : factory.Create<int>(new UnmanagedTag());
            this.unmanagedOtherStore=useStorage
                ? factory.Create<int>(this.logB!,this.objB!,new UnmanagedTag())
                : factory.Create<int>(new UnmanagedTag());
            this.unmanagedSet=this.unmanagedStore.Create();
            this.unmanagedOther=this.unmanagedOtherStore.Create();
            this.PrepareUnmanaged();
            if(this.Region==RegionKind.Immutable) {
                this.unmanagedStore.Store.Log.FlushAndEvict(wait: true);
                this.unmanagedOtherStore.Store.Log.FlushAndEvict(wait: true);
            }
        }
        else {
            this.managedStore=useStorage
                ? factory.Create<string>(this.logA!,this.objA!,new ManagedTag())
                : factory.Create<string>(new ManagedTag());
            this.managedOtherStore=useStorage
                ? factory.Create<string>(this.logB!,this.objB!,new ManagedTag())
                : factory.Create<string>(new ManagedTag());
            this.managedSet=this.managedStore.Create();
            this.managedOther=this.managedOtherStore.Create();
            this.PrepareManaged();
            if(this.Region==RegionKind.Immutable) {
                this.managedStore.Store.Log.FlushAndEvict(wait: true);
                this.managedOtherStore.Store.Log.FlushAndEvict(wait: true);
            }
        }
    }

    [IterationCleanup]
    public void IterationCleanup() {
        this.DisposeIterationResources();
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        this.DisposeIterationResources();
        if(Directory.Exists(this.checkpointRoot)) {
            Directory.Delete(this.checkpointRoot,recursive: true);
        }
    }

    [Benchmark]
    public long Run() {
        return this.Impl==CollectionImplKind.Unmanaged
            ? this.RunUnmanaged()
            : this.RunManaged();
    }

    private void PrepareUnmanaged() {
        if(this.unmanagedSet is null||this.unmanagedOther is null) {
            return;
        }
        if(this.Operation!=HashSetOperation.Add) {
            for(var i = 0; i<this.intKeys.Length; i++) {
                this.unmanagedSet.Add(this.intKeys[i]);
            }
        }
        for(var i = 0; i<this.intOtherKeys.Length; i++) {
            this.unmanagedOther.Add(this.intOtherKeys[i]);
        }
    }

    private void PrepareManaged() {
        if(this.managedSet is null||this.managedOther is null) {
            return;
        }
        if(this.Operation!=HashSetOperation.Add) {
            for(var i = 0; i<this.stringKeys.Length; i++) {
                this.managedSet.Add(this.stringKeys[i]);
            }
        }
        for(var i = 0; i<this.stringOtherKeys.Length; i++) {
            this.managedOther.Add(this.stringOtherKeys[i]);
        }
    }

    private long RunUnmanaged() {
        var set = this.unmanagedSet!;
        var other = this.unmanagedOther!;
        var coll = (ICollection<int>)set;
        var ie = (IEnumerable<int>)set;

        return this.Operation switch {
            HashSetOperation.Add => CountAdd(this.intKeys,set),
            HashSetOperation.Remove => CountRemove(this.intKeys,set),
            HashSetOperation.Contains => CountContains(this.intKeys,set),
            HashSetOperation.CopyTo => CopyToCount(coll),
            HashSetOperation.IEnumerableEnumerate => EnumerateCount(ie),
            HashSetOperation.Count => coll.Count,
            HashSetOperation.ExceptWith => ApplySetOp(() => set.ExceptWith(other),coll),
            HashSetOperation.IntersectWith => ApplySetOp(() => set.IntersectWith(other),coll),
            HashSetOperation.SymmetricExceptWith => ApplySetOp(() => set.SymmetricExceptWith(other),coll),
            HashSetOperation.UnionWith => ApplySetOp(() => set.UnionWith(other),coll),
            HashSetOperation.IsSubsetOf => set.IsSubsetOf(other) ? 1 : 0,
            HashSetOperation.IsSupersetOf => set.IsSupersetOf(other) ? 1 : 0,
            HashSetOperation.IsProperSubsetOf => set.IsProperSubsetOf(other) ? 1 : 0,
            HashSetOperation.IsProperSupersetOf => set.IsProperSupersetOf(other) ? 1 : 0,
            HashSetOperation.Overlaps => set.Overlaps(other) ? 1 : 0,
            HashSetOperation.SetEquals => set.SetEquals(other) ? 1 : 0,
            HashSetOperation.Clear => ApplySetOp(set.Clear,coll),
            _ => 0,
        };
    }

    private long RunManaged() {
        var set = this.managedSet!;
        var other = this.managedOther!;
        var coll = (ICollection<string>)set;
        var ie = (IEnumerable<string>)set;

        return this.Operation switch {
            HashSetOperation.Add => CountAdd(this.stringKeys,set),
            HashSetOperation.Remove => CountRemove(this.stringKeys,set),
            HashSetOperation.Contains => CountContains(this.stringKeys,set),
            HashSetOperation.CopyTo => CopyToCount(coll),
            HashSetOperation.IEnumerableEnumerate => EnumerateCount(ie),
            HashSetOperation.Count => coll.Count,
            HashSetOperation.ExceptWith => ApplySetOp(() => set.ExceptWith(other),coll),
            HashSetOperation.IntersectWith => ApplySetOp(() => set.IntersectWith(other),coll),
            HashSetOperation.SymmetricExceptWith => ApplySetOp(() => set.SymmetricExceptWith(other),coll),
            HashSetOperation.UnionWith => ApplySetOp(() => set.UnionWith(other),coll),
            HashSetOperation.IsSubsetOf => set.IsSubsetOf(other) ? 1 : 0,
            HashSetOperation.IsSupersetOf => set.IsSupersetOf(other) ? 1 : 0,
            HashSetOperation.IsProperSubsetOf => set.IsProperSubsetOf(other) ? 1 : 0,
            HashSetOperation.IsProperSupersetOf => set.IsProperSupersetOf(other) ? 1 : 0,
            HashSetOperation.Overlaps => set.Overlaps(other) ? 1 : 0,
            HashSetOperation.SetEquals => set.SetEquals(other) ? 1 : 0,
            HashSetOperation.Clear => ApplySetOp(set.Clear,coll),
            _ => 0,
        };
    }

    private static int CountAdd<T>(IEnumerable<T> values,IHashSet<T> set) where T : IEquatable<T> {
        var c = 0;
        foreach(var v in values) {
            if(set.Add(v)) {
                c++;
            }
        }
        return c;
    }

    private static int CountRemove<T>(IEnumerable<T> values,IHashSet<T> set) where T : IEquatable<T> {
        var c = 0;
        foreach(var v in values) {
            if(set.Remove(v)) {
                c++;
            }
        }
        return c;
    }

    private static int CountContains<T>(IEnumerable<T> values,IHashSet<T> set) where T : IEquatable<T> {
        var c = 0;
        foreach(var v in values) {
            if(set.Contains(v)) {
                c++;
            }
        }
        return c;
    }

    private static int CopyToCount<T>(ICollection<T> coll) {
        var a = new T[coll.Count];
        coll.CopyTo(a,0);
        return a.Length;
    }

    private static int EnumerateCount<T>(IEnumerable<T> ie) {
        var c = 0;
        foreach(var _ in ie) {
            c++;
        }
        return c;
    }

    private static int ApplySetOp<T>(Action op,ICollection<T> collection) {
        op();
        return collection.Count;
    }

    private void DisposeIterationResources() {
        this.unmanagedSet?.Dispose();
        this.unmanagedSet=null;
        this.unmanagedOther?.Dispose();
        this.unmanagedOther=null;
        this.managedSet?.Dispose();
        this.managedSet=null;
        this.managedOther?.Dispose();
        this.managedOther=null;

        this.unmanagedStore?.Dispose();
        this.unmanagedStore=null;
        this.unmanagedOtherStore?.Dispose();
        this.unmanagedOtherStore=null;
        this.managedStore?.Dispose();
        this.managedStore=null;
        this.managedOtherStore?.Dispose();
        this.managedOtherStore=null;

        this.logA?.Dispose();
        this.objA?.Dispose();
        this.logB?.Dispose();
        this.objB?.Dispose();
        this.logA=null;
        this.objA=null;
        this.logB=null;
        this.objB=null;
    }
}
