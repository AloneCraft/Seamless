using System.Diagnostics;
using Seamless.Collections;
using Seamless.Persisted.Collections;
using Seamless.Serialization;
using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Devices;
using Store = Seamless.Tsavorite.Collections.Store;
using TsavoriteDictionary = Seamless.Tsavorite.Collections.IDictionary<int,int>;

namespace Benchmark.Tsavorite.ParameterSweep;

public sealed class ParameterSweepRunner {
    public async Task<IReadOnlyList<ParameterSweepResult>> RunAsync(
        IReadOnlyList<ParameterSweepCase> cases,
        ParameterSweepRunOptions? options = null,
        IProgress<ParameterSweepResult>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(cases);
        var actualOptions = options??new();
        ParameterSweepPlan.ValidateOptions(actualOptions);
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff",System.Globalization.CultureInfo.InvariantCulture);
        var results = new List<ParameterSweepResult>(cases.Count);
        foreach(var item in cases) {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await Task.Run(() => this.RunCase(runId,item,actualOptions,cancellationToken),cancellationToken);
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    private ParameterSweepResult RunCase(
        string runId,
        ParameterSweepCase item,
        ParameterSweepRunOptions options,
        CancellationToken cancellationToken) {
        var measuredAt = DateTimeOffset.UtcNow;
        try {
            ParameterSweepSettingsValidator.Validate(item.Settings);
            using var workload = ParameterSweepWorkloadFactory.Create(item,options);
            return this.RunWorkload(runId,measuredAt,item,workload,options,cancellationToken);
        }
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException) {
            return CreateFailure(runId,measuredAt,item,ParameterSweepFailureKind.InvalidConfiguration,ex);
        }
        catch(OutOfMemoryException ex) {
            return CreateFailure(runId,measuredAt,item,ClassifyOutOfMemory(ex),ex);
        }
        catch(Exception ex) {
            return CreateFailure(runId,measuredAt,item,ParameterSweepFailureKind.Exception,ex);
        }
    }

    private ParameterSweepResult RunWorkload(
        string runId,
        DateTimeOffset measuredAt,
        ParameterSweepCase item,
        IParameterSweepWorkload workload,
        ParameterSweepRunOptions options,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();
        long count = 0;
        ParameterSweepFailureKind failureKind = ParameterSweepFailureKind.Completed;
        var failureMessage = string.Empty;
        try {
            while(count<options.MaxSuccessfulCount) {
                cancellationToken.ThrowIfCancellationRequested();
                if(stopwatch.Elapsed>=options.PerCaseTimeout) {
                    failureKind=ParameterSweepFailureKind.Timeout;
                    failureMessage="Per-case timeout reached.";
                    break;
                }
                workload.Add((int)count);
                count++;
            }
        }
        catch(OutOfMemoryException ex) {
            failureKind=ClassifyOutOfMemory(ex);
            failureMessage=ex.Message;
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or IOException) {
            failureKind=ParameterSweepFailureKind.Exception;
            failureMessage=ex.GetType().Name+": "+ex.Message;
        }
        stopwatch.Stop();
        double? lookupMean = count>0
            ? this.MeasureLookup(workload,count,options,cancellationToken)
            : null;
        return CreateResult(
            runId,
            measuredAt,
            item,
            count,
            workload.EntryCount,
            failureKind,
            failureMessage,
            stopwatch.Elapsed.TotalMilliseconds,
            lookupMean);
    }

    private double MeasureLookup(
        IParameterSweepWorkload workload,
        long maxSuccessfulCount,
        ParameterSweepRunOptions options,
        CancellationToken cancellationToken) {
        var probes = (int)Math.Min(maxSuccessfulCount,options.LookupProbeCount);
        if(probes<=0) {
            return 0;
        }
        var found = 0;
        var stopwatch = Stopwatch.StartNew();
        for(var index = 0;index<probes;index++) {
            cancellationToken.ThrowIfCancellationRequested();
            var valueIndex = (int)((long)index*maxSuccessfulCount/probes);
            if(workload.Lookup(valueIndex)) {
                found++;
            }
        }
        stopwatch.Stop();
        if(found!=probes) {
            throw new InvalidOperationException("Lookup did not find every inserted probe.");
        }
        return stopwatch.Elapsed.TotalMilliseconds*1_000_000/probes;
    }

    private static ParameterSweepResult CreateFailure(
        string runId,
        DateTimeOffset measuredAt,
        ParameterSweepCase item,
        ParameterSweepFailureKind failureKind,
        Exception ex) =>
        CreateResult(
            runId,
            measuredAt,
            item,
            0,
            0,
            failureKind,
            ex.GetType().Name+": "+ex.Message,
            0,
            null);

    private static ParameterSweepResult CreateResult(
        string runId,
        DateTimeOffset measuredAt,
        ParameterSweepCase item,
        long maxSuccessfulCount,
        long entryCount,
        ParameterSweepFailureKind failureKind,
        string failureMessage,
        double addElapsedMilliseconds,
        double? lookupMeanNanoseconds) =>
        new(
            runId,
            measuredAt,
            item.CaseName,
            item.Collection,
            item.GenericName,
            item.Backend,
            item.Sweep,
            item.Settings.IndexSize,
            item.Settings.PageSize,
            item.Settings.SegmentSize,
            item.Settings.MemorySize,
            item.Settings.MutableFraction,
            maxSuccessfulCount,
            entryCount,
            failureKind,
            failureMessage,
            addElapsedMilliseconds,
            lookupMeanNanoseconds);

    private static ParameterSweepFailureKind ClassifyOutOfMemory(OutOfMemoryException ex) {
        var message = ex.Message;
        return message.Contains("log",StringComparison.OrdinalIgnoreCase)
            ||message.Contains("Tsavorite",StringComparison.OrdinalIgnoreCase)
            ? ParameterSweepFailureKind.LogFull
            : ParameterSweepFailureKind.OutOfMemory;
    }
}

internal static class ParameterSweepSettingsValidator {
    public static void Validate(ParameterSweepSettings settings) {
        ValidatePowerOfTwo(settings.IndexSize,nameof(settings.IndexSize));
        ValidatePowerOfTwo(settings.PageSize,nameof(settings.PageSize));
        ValidatePowerOfTwo(settings.SegmentSize,nameof(settings.SegmentSize));
        ValidatePowerOfTwo(settings.MemorySize,nameof(settings.MemorySize));
        if(settings.IndexSize<64) {
            throw new ArgumentOutOfRangeException(nameof(settings.IndexSize));
        }
        if(settings.PageSize<4096) {
            throw new ArgumentOutOfRangeException(nameof(settings.PageSize));
        }
        if(settings.PageSize>settings.SegmentSize) {
            throw new ArgumentOutOfRangeException(nameof(settings.SegmentSize));
        }
        if(settings.MemorySize<2*settings.PageSize) {
            throw new ArgumentOutOfRangeException(nameof(settings.MemorySize));
        }
        if(double.IsNaN(settings.MutableFraction)||settings.MutableFraction<0||settings.MutableFraction>1) {
            throw new ArgumentOutOfRangeException(nameof(settings.MutableFraction));
        }
    }

    private static void ValidatePowerOfTwo(long value,string name) {
        if(value<=0||(value&(value-1))!=0) {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

internal interface IParameterSweepWorkload:IDisposable {
    long EntryCount { get; }
    void Add(int index);
    bool Lookup(int index);
}

internal static class ParameterSweepWorkloadFactory {
    public static IParameterSweepWorkload Create(ParameterSweepCase item,ParameterSweepRunOptions options) =>
        item.Collection switch {
            ParameterSweepCollectionKind.HashSet => CreateHashSet(item,options),
            ParameterSweepCollectionKind.Set => CreateSet(item,options),
            ParameterSweepCollectionKind.Dictionary => CreateDictionary(item,options),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static IParameterSweepWorkload CreateHashSet(ParameterSweepCase item,ParameterSweepRunOptions options) =>
        item.GenericName switch {
            "Int32" => new HashSetWorkload<int>(item,options,static index => index),
            "String" => new HashSetWorkload<string>(item,options,static index => "K"+index),
            "UnmanagedStruct" => new HashSetWorkload<UnmanagedStruct>(item,options,static index => new() { Value=index }),
            "ManagedStruct" => new HashSetWorkload<ManagedStruct>(item,options,static index => new() { Name="N"+index }),
            "ManagedClass" => new HashSetWorkload<ManagedClass>(item,options,static index => new() { Name="C"+index }),
            _ => throw new ArgumentOutOfRangeException(nameof(item.GenericName),item.GenericName,null),
        };

    private static IParameterSweepWorkload CreateSet(ParameterSweepCase item,ParameterSweepRunOptions options) =>
        item.GenericName switch {
            "UnmanagedSetRecord" => new SetWorkload<int,ParameterSweepUnmanagedSetRecord>(item,options,static index => new(index,index)),
            "ManagedSetRecord" => new SetWorkload<int,ParameterSweepManagedSetRecord>(item,options,static index => new(index,"N"+index)),
            _ => throw new ArgumentOutOfRangeException(nameof(item.GenericName),item.GenericName,null),
        };

    private static IParameterSweepWorkload CreateDictionary(ParameterSweepCase item,ParameterSweepRunOptions options) =>
        item.GenericName switch {
            "String/String" => new DictionaryWorkload<string,string>(item,options,static index => "K"+index,static index => "V"+index),
            "String/Int32" => new DictionaryWorkload<string,int>(item,options,static index => "K"+index,static index => index),
            "Int32/String" => new DictionaryWorkload<int,string>(item,options,static index => index,static index => "V"+index),
            "Int32/Int32" => new DictionaryWorkload<int,int>(item,options,static index => index,static index => index),
            "UnmanagedStruct/UnmanagedStruct" => new DictionaryWorkload<UnmanagedStruct,UnmanagedStruct>(item,options,static index => new() { Value=index },static index => new() { Value=index }),
            "ManagedStruct/ManagedStruct" => new DictionaryWorkload<ManagedStruct,ManagedStruct>(item,options,static index => new() { Name="K"+index },static index => new() { Name="V"+index }),
            "ManagedClass/ManagedClass" => new DictionaryWorkload<ManagedClass,ManagedClass>(item,options,static index => new() { Name="K"+index },static index => new() { Name="V"+index }),
            _ => throw new ArgumentOutOfRangeException(nameof(item.GenericName),item.GenericName,null),
        };
}

internal abstract class WorkloadBase:IDisposable {
    private readonly string? storageDirectory;
    protected IDisposable? Store { get; set; }
    private bool disposed;

    protected WorkloadBase(ParameterSweepCase item,ParameterSweepRunOptions options) {
        if(item.Backend==ParameterSweepBackend.Storage) {
            var root = options.WorkDirectory??Path.Combine(Path.GetTempPath(),"Seamless","TsavoriteParameterSweep");
            this.storageDirectory=Path.Combine(root,Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.storageDirectory);
        }
    }

    public abstract long EntryCount { get; }

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        this.DisposeCore();
        this.Store?.Dispose();
        if(this.storageDirectory is not null&&Directory.Exists(this.storageDirectory)) {
            try {
                Directory.Delete(this.storageDirectory,true);
            }
            catch(IOException) {
            }
            catch(UnauthorizedAccessException) {
            }
        }
    }

    protected abstract void DisposeCore();

    protected Store CreateStore(ParameterSweepCase item,ParameterSweepRunOptions options,out TsavoriteStorageDevicePair? devices) {
        var settings = item.Settings;
        var store = Seamless.Tsavorite.Collections.Store.Create(
            IndexSize: settings.IndexSize,
            PageSize: settings.PageSize,
            SegmentSize: settings.SegmentSize,
            MemorySize: settings.MemorySize,
            MutableFraction: settings.MutableFraction);
        if(item.Backend==ParameterSweepBackend.Memory) {
            devices=null;
            return store;
        }
        var directory = this.storageDirectory??throw new InvalidOperationException("Storage directory was not initialized.");
        var tuning = new StorageTuning(
            FileAccess.ReadWrite,
            StorageAccessPattern.Sequential,
            StorageBufferingMode.Buffered);
        devices=TsavoriteStorageDevices.CreatePair(
            Path.Combine(directory,"log.data"),
            Path.Combine(directory,"objectlog.data"),
            tuning,
            deleteOnClose:true,
            capacity: options.StorageDeviceCapacity);
        return store;
    }
}

internal sealed class HashSetWorkload<TKey>:WorkloadBase,IParameterSweepWorkload
    where TKey : IEquatable<TKey> {
    private readonly Func<int,TKey> keyFactory;
    private readonly Seamless.Tsavorite.Collections.HashSet<TKey> set;

    public HashSetWorkload(ParameterSweepCase item,ParameterSweepRunOptions options,Func<int,TKey> keyFactory) : base(item,options) {
        this.keyFactory=keyFactory;
        var storeFactory = this.CreateStore(item,options,out var devices);
        try {
            var store = item.Backend==ParameterSweepBackend.Storage
                ? storeFactory.Create<TKey>(devices!.Value.LogDevice,devices.Value.ObjectLogDevice)
                : storeFactory.Create<TKey>();
            this.Store=store;
            this.set=store.Create();
        }
        catch {
            devices?.Dispose();
            throw;
        }
    }

    public override long EntryCount => this.set.EntryCount;

    public void Add(int index) => this.set.Add(this.keyFactory(index));

    public bool Lookup(int index) => this.set.Contains(this.keyFactory(index));

    protected override void DisposeCore() => this.set.Dispose();
}

internal sealed class SetWorkload<TKey,TRecord>:WorkloadBase,IParameterSweepWorkload
    where TKey : IEquatable<TKey>
    where TRecord : IRecord<TKey> {
    private readonly Func<int,TRecord> recordFactory;
    private readonly Seamless.Tsavorite.Collections.Set<TKey,TRecord> set;

    public SetWorkload(ParameterSweepCase item,ParameterSweepRunOptions options,Func<int,TRecord> recordFactory) : base(item,options) {
        this.recordFactory=recordFactory;
        var storeFactory = this.CreateStore(item,options,out var devices);
        try {
            var store = item.Backend==ParameterSweepBackend.Storage
                ? storeFactory.CreateSet<TKey,TRecord>(devices!.Value.LogDevice,devices.Value.ObjectLogDevice)
                : storeFactory.CreateSet<TKey,TRecord>();
            this.Store=store;
            this.set=store.Create();
        }
        catch {
            devices?.Dispose();
            throw;
        }
    }

    public override long EntryCount => this.set.EntryCount;

    public void Add(int index) => this.set.Add(this.recordFactory(index));

    public bool Lookup(int index) => this.set.ContainsKey(this.recordFactory(index).Key);

    protected override void DisposeCore() => this.set.Dispose();
}

internal sealed class DictionaryWorkload<TKey,TValue>:WorkloadBase,IParameterSweepWorkload
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private readonly Func<int,TKey> keyFactory;
    private readonly Func<int,TValue> valueFactory;
    private readonly Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> dictionary;

    public DictionaryWorkload(
        ParameterSweepCase item,
        ParameterSweepRunOptions options,
        Func<int,TKey> keyFactory,
        Func<int,TValue> valueFactory) : base(item,options) {
        this.keyFactory=keyFactory;
        this.valueFactory=valueFactory;
        var storeFactory = this.CreateStore(item,options,out var devices);
        try {
            var store = item.Backend==ParameterSweepBackend.Storage
                ? storeFactory.Create<TKey,TValue>(devices!.Value.LogDevice,devices.Value.ObjectLogDevice)
                : storeFactory.Create<TKey,TValue>();
            this.Store=store;
            this.dictionary=store.Create();
        }
        catch {
            devices?.Dispose();
            throw;
        }
    }

    public override long EntryCount => this.dictionary.EntryCount;

    public void Add(int index) => this.dictionary.Add(this.keyFactory(index),this.valueFactory(index));

    public bool Lookup(int index) => this.dictionary.TryGetValue(this.keyFactory(index),out _);

    protected override void DisposeCore() => this.dictionary.Dispose();
}
