using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Collections;
using Tsavorite.Stores;
using Tsavorite.core;

namespace Tsavorite.Benchmark.Benchmarks;

public enum StorageMaintenanceOperation {
    BulkInsertSegments,
    ChurnWithCompaction,
    RefreshSiblingChurn,
}

internal sealed class StorageBenchmarkWorkspace:IDisposable {
    public string DirectoryPath { get; }
    public IDevice LogDevice { get; }
    public IDevice ObjectLogDevice { get; }

    private StorageBenchmarkWorkspace(string directoryPath,IDevice logDevice,IDevice objectLogDevice) {
        this.DirectoryPath=directoryPath;
        this.LogDevice=logDevice;
        this.ObjectLogDevice=objectLogDevice;
    }

    public static StorageBenchmarkWorkspace Create(string root) {
        var directoryPath = Path.Combine(root,"iter-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return new(
            directoryPath,
            global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(directoryPath,"log"),deleteOnClose: false),
            global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(directoryPath,"obj"),deleteOnClose: false));
    }

    public int CountSegmentFiles() =>
        Directory.GetFiles(this.DirectoryPath,"log.*").Length;

    public void Dispose() {
        this.LogDevice.Dispose();
        this.ObjectLogDevice.Dispose();
        if(Directory.Exists(this.DirectoryPath)) {
            try { Directory.Delete(this.DirectoryPath,recursive: true); } catch { }
        }
    }
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class HashSetStorageMaintenanceBenchmark {
    private int[] intKeys = null!;
    private string[] stringKeys = null!;
    private string workspaceRoot = null!;

    [Params(10_000)]
    public int Count { get; set; }

    [Params(CollectionImplKind.Managed,CollectionImplKind.Blittable)]
    public CollectionImplKind Impl { get; set; }

    [Params(
        StorageMaintenanceOperation.BulkInsertSegments,
        StorageMaintenanceOperation.ChurnWithCompaction,
        StorageMaintenanceOperation.RefreshSiblingChurn)]
    public StorageMaintenanceOperation Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.intKeys=new int[this.Count];
        this.stringKeys=new string[this.Count];
        for(var i = 0; i<this.Count; i++) {
            this.intKeys[i]=i;
            this.stringKeys[i]="hashset_key_"+i.ToString("D6")+"_"+new string('a',50);
        }

        this.workspaceRoot=BenchmarkOutputLayout.CreateCategoryWorkspaceRoot("HashSet");
        Directory.CreateDirectory(this.workspaceRoot);
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        if(Directory.Exists(this.workspaceRoot)) {
            Directory.Delete(this.workspaceRoot,recursive: true);
        }
    }

    [Benchmark]
    public long Run() =>
        this.Impl==CollectionImplKind.Blittable
            ? this.RunBlittable()
            : this.RunManaged();

    private long RunBlittable() {
        using var workspace = StorageBenchmarkWorkspace.Create(this.workspaceRoot);
        using var store = CreateFactory().Create<int>(workspace.LogDevice,workspace.ObjectLogDevice,new BlittableStrategy());
        return this.Operation switch {
            StorageMaintenanceOperation.BulkInsertSegments => BulkInsertSegments(store,store.Create(),this.intKeys,workspace),
            StorageMaintenanceOperation.ChurnWithCompaction => ChurnWithCompaction(store,store.Create(),this.intKeys,workspace),
            StorageMaintenanceOperation.RefreshSiblingChurn => RefreshSiblingChurn(store,this.intKeys,workspace),
            _ => 0,
        };
    }

    private long RunManaged() {
        using var workspace = StorageBenchmarkWorkspace.Create(this.workspaceRoot);
        using var store = CreateFactory().Create<string>(workspace.LogDevice,workspace.ObjectLogDevice,new MemoryPackSerializerStrategy());
        return this.Operation switch {
            StorageMaintenanceOperation.BulkInsertSegments => BulkInsertSegments(store,store.Create(),this.stringKeys,workspace),
            StorageMaintenanceOperation.ChurnWithCompaction => ChurnWithCompaction(store,store.Create(),this.stringKeys,workspace),
            StorageMaintenanceOperation.RefreshSiblingChurn => RefreshSiblingChurn(store,this.stringKeys,workspace),
            _ => 0,
        };
    }

    private static HashStore CreateFactory() =>
        HashStore.Create(
            IndexSize: 64,
            PageSize: 4096,
            SegmentSize: 4096,
            MemorySize: 16384,
            MutableFraction: 0.05);

    private static long BulkInsertSegments<TKey>(HashSetStore<TKey> store,IHashSet<TKey> set,TKey[] keys,StorageBenchmarkWorkspace workspace)
        where TKey : IEquatable<TKey> {
        using(set) {
            for(var i = 0; i<keys.Length; i++) {
                set.Add(keys[i]);
            }

            store.Store.Log.Flush(wait: true);
            return set.Count+workspace.CountSegmentFiles();
        }
    }

    private static long ChurnWithCompaction<TKey>(HashSetStore<TKey> store,IHashSet<TKey> set,TKey[] keys,StorageBenchmarkWorkspace workspace)
        where TKey : IEquatable<TKey> {
        using(set) {
            var workingSet = GetWorkingSet(keys.Length);
            for(var i = 0; i<workingSet; i++) {
                set.Add(keys[i]);
            }

            for(var i = workingSet; i<keys.Length; i++) {
                set.Add(keys[i]);
                set.Remove(keys[i-workingSet]);
            }

            store.Store.Log.Flush(wait: true);
            var before = store.Address;
            var activeBefore = CountActiveSegments(before);

            store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
            store.CompactWithLookup();
            store.Store.Log.FlushAndEvict(wait: true);

            var after = store.Address;
            var activeAfter = CountActiveSegments(after);
            return set.Count+workspace.CountSegmentFiles()+Math.Max(0,activeBefore-activeAfter)+(after.BeginAddress-before.BeginAddress);
        }
    }

    private static long RefreshSiblingChurn<TKey>(HashSetStore<TKey> store,TKey[] keys,StorageBenchmarkWorkspace workspace)
        where TKey : IEquatable<TKey> {
        using var owner = store.Create();
        using var sibling1 = store.Create();
        using var sibling2 = store.Create();

        var workingSet = GetWorkingSet(keys.Length);
        for(var i = 0; i<workingSet; i++) {
            owner.Add(keys[i]);
        }

        for(var i = workingSet; i<keys.Length; i++) {
            owner.Add(keys[i]);
            owner.Remove(keys[i-workingSet]);
        }

        owner.Refresh();
        sibling1.Refresh();
        store.Store.Log.Flush(wait: true);
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
        store.CompactWithLookup();
        store.Store.Log.FlushAndEvict(wait: true);

        var beginBeforeFinalRefresh = store.Address.BeginAddress;
        var liveKey = keys[^1];
        var observedBefore = sibling1.Contains(liveKey) ? 1 : 0;

        sibling2.Refresh();
        var observedAfter = sibling2.Contains(liveKey) ? 1 : 0;
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
        store.CompactWithLookup();
        store.Store.Log.FlushAndEvict(wait: true);

        return owner.Count+observedBefore+observedAfter+workspace.CountSegmentFiles()+(store.Address.BeginAddress-beginBeforeFinalRefresh);
    }

    private static int GetWorkingSet(int count) =>
        Math.Max(1,Math.Min(1024,count/4));

    private static int CountActiveSegments((long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) address) {
        var bytesInUse = Math.Max(0,address.TailAddress-address.BeginAddress);
        return Math.Max(1,(int)((bytesInUse+4095)/4096));
    }
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class DictionaryStorageMaintenanceBenchmark {
    private int[] intKeys = null!;
    private string[] stringKeys = null!;
    private string[] stringValues = null!;
    private string workspaceRoot = null!;

    [Params(10_000)]
    public int Count { get; set; }

    [Params(CollectionImplKind.Managed,CollectionImplKind.Blittable)]
    public CollectionImplKind Impl { get; set; }

    [Params(
        StorageMaintenanceOperation.BulkInsertSegments,
        StorageMaintenanceOperation.ChurnWithCompaction,
        StorageMaintenanceOperation.RefreshSiblingChurn)]
    public StorageMaintenanceOperation Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.intKeys=new int[this.Count];
        this.stringKeys=new string[this.Count];
        this.stringValues=new string[this.Count];
        for(var i = 0; i<this.Count; i++) {
            this.intKeys[i]=i;
            this.stringKeys[i]="key_"+i.ToString("D6");
            this.stringValues[i]="value_"+i.ToString("D6")+"_"+new string('x',256);
        }

        this.workspaceRoot=BenchmarkOutputLayout.CreateCategoryWorkspaceRoot("Dictionary");
        Directory.CreateDirectory(this.workspaceRoot);
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        if(Directory.Exists(this.workspaceRoot)) {
            Directory.Delete(this.workspaceRoot,recursive: true);
        }
    }

    [Benchmark]
    public long Run() =>
        this.Impl==CollectionImplKind.Blittable
            ? this.RunBlittable()
            : this.RunManaged();

    private long RunBlittable() {
        using var workspace = StorageBenchmarkWorkspace.Create(this.workspaceRoot);
        using var store = CreateFactory().Create<int,int>(workspace.LogDevice,workspace.ObjectLogDevice,new BlittableStrategy(),new BlittableStrategy());
        return this.Operation switch {
            StorageMaintenanceOperation.BulkInsertSegments => BulkInsertSegments(store,store.Create(),this.intKeys,this.intKeys,workspace),
            StorageMaintenanceOperation.ChurnWithCompaction => ChurnWithCompaction(store,store.Create(),this.intKeys,this.intKeys,workspace),
            StorageMaintenanceOperation.RefreshSiblingChurn => RefreshSiblingChurn(store,this.intKeys,this.intKeys,workspace),
            _ => 0,
        };
    }

    private long RunManaged() {
        using var workspace = StorageBenchmarkWorkspace.Create(this.workspaceRoot);
        using var store = CreateFactory().Create<string,string>(workspace.LogDevice,workspace.ObjectLogDevice,new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        return this.Operation switch {
            StorageMaintenanceOperation.BulkInsertSegments => BulkInsertSegments(store,store.Create(),this.stringKeys,this.stringValues,workspace),
            StorageMaintenanceOperation.ChurnWithCompaction => ChurnWithCompaction(store,store.Create(),this.stringKeys,this.stringValues,workspace),
            StorageMaintenanceOperation.RefreshSiblingChurn => RefreshSiblingChurn(store,this.stringKeys,this.stringValues,workspace),
            _ => 0,
        };
    }

    private static HashStore CreateFactory() =>
        HashStore.Create(
            IndexSize: 64,
            PageSize: 4096,
            SegmentSize: 4096,
            MemorySize: 16384,
            MutableFraction: 0.05);

    private static long BulkInsertSegments<TKey,TValue>(
        DictionaryStore<TKey,TValue> store,
        global::Tsavorite.Collections.IDictionary<TKey,TValue> dict,
        TKey[] keys,
        TValue[] values,
        StorageBenchmarkWorkspace workspace)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        using(dict) {
            for(var i = 0; i<keys.Length; i++) {
                dict.Add(keys[i],values[i]);
            }

            store.Store.Log.Flush(wait: true);
            return dict.Count+workspace.CountSegmentFiles();
        }
    }

    private static long ChurnWithCompaction<TKey,TValue>(
        DictionaryStore<TKey,TValue> store,
        global::Tsavorite.Collections.IDictionary<TKey,TValue> dict,
        TKey[] keys,
        TValue[] values,
        StorageBenchmarkWorkspace workspace)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        using(dict) {
            var workingSet = GetWorkingSet(keys.Length);
            for(var i = 0; i<workingSet; i++) {
                dict.Add(keys[i],values[i]);
            }

            for(var i = workingSet; i<keys.Length; i++) {
                dict[keys[i]]=values[i];
                dict.Remove(keys[i-workingSet]);
            }

            store.Store.Log.Flush(wait: true);
            var before = store.Address;
            var activeBefore = CountActiveSegments(before);

            store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
            store.CompactWithLookup();
            store.Store.Log.FlushAndEvict(wait: true);

            var after = store.Address;
            var activeAfter = CountActiveSegments(after);
            return dict.Count+workspace.CountSegmentFiles()+Math.Max(0,activeBefore-activeAfter)+(after.BeginAddress-before.BeginAddress);
        }
    }

    private static long RefreshSiblingChurn<TKey,TValue>(
        DictionaryStore<TKey,TValue> store,
        TKey[] keys,
        TValue[] values,
        StorageBenchmarkWorkspace workspace)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        using var owner = store.Create();
        using var sibling1 = store.Create();
        using var sibling2 = store.Create();

        var workingSet = GetWorkingSet(keys.Length);
        for(var i = 0; i<workingSet; i++) {
            owner.Add(keys[i],values[i]);
        }

        for(var i = workingSet; i<keys.Length; i++) {
            owner[keys[i]]=values[i];
            owner.Remove(keys[i-workingSet]);
        }

        owner.Refresh();
        sibling1.Refresh();
        store.Store.Log.Flush(wait: true);
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
        store.CompactWithLookup();
        store.Store.Log.FlushAndEvict(wait: true);

        var beginBeforeFinalRefresh = store.Address.BeginAddress;
        var liveKey = keys[^1];
        var observedBefore = sibling1.TryGetValue(liveKey,out _) ? 1 : 0;

        sibling2.Refresh();
        var observedAfter = sibling2.TryGetValue(liveKey,out _) ? 1 : 0;
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
        store.CompactWithLookup();
        store.Store.Log.FlushAndEvict(wait: true);

        return owner.Count+observedBefore+observedAfter+workspace.CountSegmentFiles()+(store.Address.BeginAddress-beginBeforeFinalRefresh);
    }

    private static int GetWorkingSet(int count) =>
        Math.Max(1,Math.Min(1024,count/4));

    private static int CountActiveSegments((long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) address) {
        var bytesInUse = Math.Max(0,address.TailAddress-address.BeginAddress);
        return Math.Max(1,(int)((bytesInUse+4095)/4096));
    }
}
