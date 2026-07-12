using System.Diagnostics;
using System.Collections.Concurrent;
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
            var result = await this.RunCaseAsync(runId,item,actualOptions,cancellationToken);
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    public Task<ParameterSweepResult> RunCaseAsync(
        ParameterSweepCase item,
        ParameterSweepRunOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(item);
        var actualOptions = options??new();
        ParameterSweepPlan.ValidateOptions(actualOptions);
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff",System.Globalization.CultureInfo.InvariantCulture);
        return this.RunCaseAsync(runId,item,actualOptions,cancellationToken);
    }

    private Task<ParameterSweepResult> RunCaseAsync(
        string runId,
        ParameterSweepCase item,
        ParameterSweepRunOptions options,
        CancellationToken cancellationToken) => Task.Run(() => this.RunCase(runId,item,options,cancellationToken),CancellationToken.None);

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
        var cpuNumbers = options.LogicalCpus.Count==0 ? [null] : options.LogicalCpus.Select(static cpu => (int?)cpu).ToArray();
        var workers = cpuNumbers.Select((cpu,index) => new ParameterSweepWorkerExecution(index,cpu,workload.CreateWorker())).ToArray();
        foreach(var worker in workers) {
            worker.WorkerCount=workers.Length;
        }
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var addStarted = new ManualResetEventSlim(false);
        var addCompleted = new CountdownEvent(workers.Length);
        var lookupStarted = new ManualResetEventSlim(false);
        var lookupCompleted = new CountdownEvent(workers.Length);
        var ready = new CountdownEvent(workers.Length);
        var failures = new ConcurrentQueue<ParameterSweepWorkerFailure>();
        var startedAt = Stopwatch.StartNew();
        var threads = workers.Select(worker => this.CreateWorkerThread(
            worker,
            options,
            startedAt,
            linkedCancellation,
            ready,
            addStarted,
            addCompleted,
            lookupStarted,
            lookupCompleted,
            failures)).ToArray();
        foreach(var thread in threads) {
            thread.Start();
        }
        ready.Wait(CancellationToken.None);
        var addStopwatch = Stopwatch.StartNew();
        addStarted.Set();
        addCompleted.Wait(CancellationToken.None);
        addStopwatch.Stop();
        var lookupStopwatch = Stopwatch.StartNew();
        lookupStarted.Set();
        lookupCompleted.Wait(CancellationToken.None);
        lookupStopwatch.Stop();
        foreach(var thread in threads) {
            thread.Join();
        }
        var workerMetrics = workers.Select(worker => worker.CreateMetric()).ToArray();
        var firstFailure = failures.TryPeek(out var failure) ? failure : null;
        var count = workerMetrics.Sum(static metric => metric.AddOperationCount);
        var failureKind = firstFailure?.Kind??ParameterSweepFailureKind.Completed;
        var failureMessage = firstFailure?.Message??string.Empty;
        var lookupCount = workerMetrics.Sum(static metric => metric.LookupOperationCount);
        double? lookupMean = lookupCount>0&&failureKind==ParameterSweepFailureKind.Completed
            ? lookupStopwatch.Elapsed.TotalMilliseconds*1_000_000/lookupCount
            : null;
        return CreateResult(
            runId,
            measuredAt,
            item,
            count,
            workload.EntryCount,
            failureKind,
            failureMessage,
            addStopwatch.Elapsed.TotalMilliseconds,
            lookupMean) with {
                WorkerMetrics=workerMetrics,
                CpuSetKey=string.Join(",",cpuNumbers.Where(static cpu => cpu is not null).Select(static cpu => cpu!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                ConcurrencyLevel=workers.Length,
            };
    }

    private Thread CreateWorkerThread(
        ParameterSweepWorkerExecution worker,
        ParameterSweepRunOptions options,
        Stopwatch caseStopwatch,
        CancellationTokenSource cancellation,
        CountdownEvent ready,
        ManualResetEventSlim addStarted,
        CountdownEvent addCompleted,
        ManualResetEventSlim lookupStarted,
        CountdownEvent lookupCompleted,
        ConcurrentQueue<ParameterSweepWorkerFailure> failures) => new(() => {
            try {
                ParameterSweepCpuAffinity.TryApplyCurrentThread(worker.LogicalCpu);
                ready.Signal();
                addStarted.Wait();
                this.RunAddPhase(worker,options,caseStopwatch,cancellation,failures);
                addCompleted.Signal();
                lookupStarted.Wait();
                this.RunLookupPhase(worker,options,caseStopwatch,cancellation,failures);
            }
            catch(Exception ex) {
                this.RecordFailure(worker,ClassifyFailure(ex),ex.Message,cancellation,failures);
                if(ready.CurrentCount>0) {
                    ready.Signal();
                }
                if(addCompleted.CurrentCount>0) {
                    addCompleted.Signal();
                }
            }
            finally {
                lookupCompleted.Signal();
                worker.Dispose();
            }
        }) {
            IsBackground=true,
            Name="TsavoriteParameterSweep"+(worker.LogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"Worker"),
        };

    private void RunAddPhase(
        ParameterSweepWorkerExecution worker,
        ParameterSweepRunOptions options,
        Stopwatch caseStopwatch,
        CancellationTokenSource cancellation,
        ConcurrentQueue<ParameterSweepWorkerFailure> failures) {
        if(cancellation.IsCancellationRequested) {
            return;
        }
        var stopwatch = Stopwatch.StartNew();
        try {
            var range = CreateRange(options.MaxSuccessfulCount,worker.Index,worker.WorkerCount);
            for(var index=range.Start;index<range.End;index++) {
                this.ThrowIfStopped(caseStopwatch,options,cancellation.Token);
                worker.Workload.Add((int)index);
                worker.AddOperationCount++;
            }
        }
        catch(Exception ex) {
            this.RecordFailure(worker,ClassifyFailure(ex),ex.Message,cancellation,failures);
        }
        finally {
            stopwatch.Stop();
            worker.AddElapsedMilliseconds=stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    private void RunLookupPhase(
        ParameterSweepWorkerExecution worker,
        ParameterSweepRunOptions options,
        Stopwatch caseStopwatch,
        CancellationTokenSource cancellation,
        ConcurrentQueue<ParameterSweepWorkerFailure> failures) {
        if(cancellation.IsCancellationRequested) {
            return;
        }
        var probes = Math.Min(options.MaxSuccessfulCount,options.LookupProbeCount);
        var stopwatch = Stopwatch.StartNew();
        try {
            var range = CreateRange(probes,worker.Index,worker.WorkerCount);
            for(var probe=range.Start;probe<range.End;probe++) {
                this.ThrowIfStopped(caseStopwatch,options,cancellation.Token);
                var valueIndex = (int)(probe*options.MaxSuccessfulCount/probes);
                if(!worker.Workload.Lookup(valueIndex)) {
                    throw new InvalidOperationException("Lookup did not find every inserted probe.");
                }
                worker.LookupOperationCount++;
            }
        }
        catch(Exception ex) {
            this.RecordFailure(worker,ClassifyFailure(ex),ex.Message,cancellation,failures);
        }
        finally {
            stopwatch.Stop();
            worker.LookupElapsedMilliseconds=stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    private void ThrowIfStopped(Stopwatch stopwatch,ParameterSweepRunOptions options,CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if(stopwatch.Elapsed>=options.PerCaseTimeout) {
            throw new TimeoutException("Per-case timeout reached.");
        }
    }

    private void RecordFailure(
        ParameterSweepWorkerExecution worker,
        ParameterSweepFailureKind failureKind,
        string message,
        CancellationTokenSource cancellation,
        ConcurrentQueue<ParameterSweepWorkerFailure> failures) {
        worker.FailureKind=failureKind;
        worker.FailureMessage=message;
        failures.Enqueue(new(failureKind,message));
        cancellation.Cancel();
    }

    private static ParameterSweepRange CreateRange(long total,int workerIndex,int workerCount) {
        var start=total*workerIndex/workerCount;
        var end=total*(workerIndex+1)/workerCount;
        return new(start,end);
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

    private static ParameterSweepFailureKind ClassifyFailure(Exception ex) => ex switch {
        TimeoutException => ParameterSweepFailureKind.Timeout,
        OutOfMemoryException outOfMemory => ClassifyOutOfMemory(outOfMemory),
        ArgumentException or NotSupportedException => ParameterSweepFailureKind.InvalidConfiguration,
        _ => ParameterSweepFailureKind.Exception,
    };

    private readonly record struct ParameterSweepRange(long Start,long End);

    private sealed record ParameterSweepWorkerFailure(ParameterSweepFailureKind Kind,string Message);

    private sealed class ParameterSweepWorkerExecution(int index,int? logicalCpu,IParameterSweepWorker workload):IDisposable {
        public int Index { get; } = index;
        public int? LogicalCpu { get; } = logicalCpu;
        public IParameterSweepWorker Workload { get; } = workload;
        public int WorkerCount { get; set; }
        public long AddOperationCount { get; set; }
        public double AddElapsedMilliseconds { get; set; }
        public long LookupOperationCount { get; set; }
        public double LookupElapsedMilliseconds { get; set; }
        public ParameterSweepFailureKind FailureKind { get; set; } = ParameterSweepFailureKind.Completed;
        public string FailureMessage { get; set; } = string.Empty;

        public ParameterSweepWorkerMetric CreateMetric() => new(
            this.LogicalCpu,
            this.AddOperationCount,
            this.AddElapsedMilliseconds,
            this.LookupOperationCount,
            this.LookupElapsedMilliseconds,
            this.FailureKind,
            this.FailureMessage);

        public void Dispose() => this.Workload.Dispose();
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
    IParameterSweepWorker CreateWorker();
}

internal interface IParameterSweepWorker:IDisposable {
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
    private readonly Seamless.Tsavorite.Stores.HashSetStore<TKey> store;

    public HashSetWorkload(ParameterSweepCase item,ParameterSweepRunOptions options,Func<int,TKey> keyFactory) : base(item,options) {
        this.keyFactory=keyFactory;
        var storeFactory = this.CreateStore(item,options,out var devices);
        try {
            this.store = item.Backend==ParameterSweepBackend.Storage
                ? storeFactory.Create<TKey>(devices!.Value.LogDevice,devices.Value.ObjectLogDevice)
                : storeFactory.Create<TKey>();
            this.Store=this.store;
        }
        catch {
            devices?.Dispose();
            throw;
        }
    }

    public override long EntryCount => this.store.Store.EntryCount;

    public IParameterSweepWorker CreateWorker() => new HashSetWorker<TKey>(this.store.Create(),this.keyFactory);

    protected override void DisposeCore() {
    }
}

internal sealed class SetWorkload<TKey,TRecord>:WorkloadBase,IParameterSweepWorkload
    where TKey : IEquatable<TKey>
    where TRecord : IRecord<TKey> {
    private readonly Func<int,TRecord> recordFactory;
    private readonly Seamless.Tsavorite.Stores.SetStore<TKey,TRecord> store;

    public SetWorkload(ParameterSweepCase item,ParameterSweepRunOptions options,Func<int,TRecord> recordFactory) : base(item,options) {
        this.recordFactory=recordFactory;
        var storeFactory = this.CreateStore(item,options,out var devices);
        try {
            this.store = item.Backend==ParameterSweepBackend.Storage
                ? storeFactory.CreateSet<TKey,TRecord>(devices!.Value.LogDevice,devices.Value.ObjectLogDevice)
                : storeFactory.CreateSet<TKey,TRecord>();
            this.Store=this.store;
        }
        catch {
            devices?.Dispose();
            throw;
        }
    }

    public override long EntryCount => this.store.Store.EntryCount;

    public IParameterSweepWorker CreateWorker() => new SetWorker<TKey,TRecord>(this.store.Create(),this.recordFactory);

    protected override void DisposeCore() {
    }
}

internal sealed class DictionaryWorkload<TKey,TValue>:WorkloadBase,IParameterSweepWorkload
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private readonly Func<int,TKey> keyFactory;
    private readonly Func<int,TValue> valueFactory;
    private readonly Seamless.Tsavorite.Stores.DictionaryStore<TKey,TValue> store;

    public DictionaryWorkload(
        ParameterSweepCase item,
        ParameterSweepRunOptions options,
        Func<int,TKey> keyFactory,
        Func<int,TValue> valueFactory) : base(item,options) {
        this.keyFactory=keyFactory;
        this.valueFactory=valueFactory;
        var storeFactory = this.CreateStore(item,options,out var devices);
        try {
            this.store = item.Backend==ParameterSweepBackend.Storage
                ? storeFactory.Create<TKey,TValue>(devices!.Value.LogDevice,devices.Value.ObjectLogDevice)
                : storeFactory.Create<TKey,TValue>();
            this.Store=this.store;
        }
        catch {
            devices?.Dispose();
            throw;
        }
    }

    public override long EntryCount => this.store.Store.EntryCount;

    public IParameterSweepWorker CreateWorker() => new DictionaryWorker<TKey,TValue>(this.store.Create(),this.keyFactory,this.valueFactory);

    protected override void DisposeCore() {
    }
}

internal sealed class HashSetWorker<TKey>(Seamless.Tsavorite.Collections.HashSet<TKey> set,Func<int,TKey> keyFactory):IParameterSweepWorker
    where TKey:IEquatable<TKey> {
    public void Add(int index) => set.Add(keyFactory(index));

    public bool Lookup(int index) => set.Contains(keyFactory(index));

    public void Dispose() => set.Dispose();
}

internal sealed class SetWorker<TKey,TRecord>(Seamless.Tsavorite.Collections.Set<TKey,TRecord> set,Func<int,TRecord> recordFactory):IParameterSweepWorker
    where TKey:IEquatable<TKey>
    where TRecord:IRecord<TKey> {
    public void Add(int index) => set.Add(recordFactory(index));

    public bool Lookup(int index) => set.ContainsKey(recordFactory(index).Key);

    public void Dispose() => set.Dispose();
}

internal sealed class DictionaryWorker<TKey,TValue>(Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> dictionary,Func<int,TKey> keyFactory,Func<int,TValue> valueFactory):IParameterSweepWorker
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    public void Add(int index) => dictionary.Add(keyFactory(index),valueFactory(index));

    public bool Lookup(int index) => dictionary.TryGetValue(keyFactory(index),out _);

    public void Dispose() => dictionary.Dispose();
}

internal static class ParameterSweepCpuAffinity {
    public static void TryApplyCurrentThread(int? logicalCpu) {
        if(logicalCpu is not { } cpu||cpu<0) {
            return;
        }
        if(OperatingSystem.IsWindows()) {
            TryApplyWindows(cpu);
        }else if(OperatingSystem.IsLinux()) {
            TryApplyLinux(cpu);
        }
    }

    private static void TryApplyWindows(int logicalCpu) {
        try {
            var remaining=logicalCpu;
            var groupCount=GetActiveProcessorGroupCount();
            for(ushort group=0;group<groupCount;group++) {
                var count=(int)GetActiveProcessorCount(group);
                if(remaining>=count) {
                    remaining-=count;
                    continue;
                }
                var affinity=new GroupAffinity {
                    Mask=new UIntPtr(1UL<<remaining),
                    Group=group,
                };
                _=SetThreadGroupAffinity(GetCurrentThread(),ref affinity,IntPtr.Zero);
                return;
            }
        }
        catch(DllNotFoundException) {
        }
        catch(EntryPointNotFoundException) {
        }
    }

    private static void TryApplyLinux(int logicalCpu) {
        var size=Math.Max(8,(logicalCpu/8)+1);
        var cpuSet=System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try {
            for(var index=0;index<size;index++) {
                System.Runtime.InteropServices.Marshal.WriteByte(cpuSet,index,0);
            }
            var byteIndex=logicalCpu/8;
            var current=System.Runtime.InteropServices.Marshal.ReadByte(cpuSet,byteIndex);
            System.Runtime.InteropServices.Marshal.WriteByte(cpuSet,byteIndex,(byte)(current|(1<<(logicalCpu%8))));
            _=sched_setaffinity(0,(nuint)size,cpuSet);
        }
        catch(DllNotFoundException) {
        }
        catch(EntryPointNotFoundException) {
        }
        finally {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(cpuSet);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]
    private static extern bool SetThreadGroupAffinity(IntPtr threadHandle,ref GroupAffinity groupAffinity,IntPtr previousGroupAffinity);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern ushort GetActiveProcessorGroupCount();

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetActiveProcessorCount(ushort groupNumber);

    [System.Runtime.InteropServices.DllImport("libc",SetLastError=true)]
    private static extern int sched_setaffinity(int pid,nuint cpusetsize,IntPtr mask);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct GroupAffinity {
        public UIntPtr Mask;
        public ushort Group;
        public ushort Reserved0;
        public ushort Reserved1;
        public ushort Reserved2;
    }
}
