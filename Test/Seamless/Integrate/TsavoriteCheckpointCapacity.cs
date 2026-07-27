using Seamless.Collections.Tsavorite;
using Seamless.Collections.Tsavorite;

using Tsavorite.core;

namespace Test.Seamless.Integrate;

[Collection("IntegrateTests")]
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class TsavoriteCheckpointCapacity(ITestOutputHelper output) {
    private readonly ITestOutputHelper output = output;
    private const long SingleStoreCapacity = 128L*1024*1024;
    private const long SingleStoreSegmentSize = 16L*1024*1024;
    private const long SingleStoreIndexSize = 1L<<20;
    private const long MinIndexSize = 64;
    private const long MinPageSize = 1L<<12;
    private const long MinSegmentSize = 1L<<12;
    private const long MinMemorySize = 1L<<13;
    private const int SmallCount = 64;
    private const int StoreCount = 1024;

    public static IEnumerable<object[]> CheckpointModes() {
        yield return [new CheckpointMode("SaveSnapshot",CheckpointSaveKind.Full,CheckpointType.Snapshot)];
        yield return [new CheckpointMode("SaveFoldOver",CheckpointSaveKind.Full,CheckpointType.FoldOver)];
        yield return [new CheckpointMode("SaveLogOnlySnapshot",CheckpointSaveKind.LogOnly,CheckpointType.Snapshot)];
        yield return [new CheckpointMode("SaveLogOnlyFoldOver",CheckpointSaveKind.LogOnly,CheckpointType.FoldOver)];
    }

    [Theory]
    [MemberData(nameof(CheckpointModes))]
    public void Save_Load_RestoresAcrossDeviceStates(CheckpointMode mode) {
        this.RunSmallRecoveryScenario(mode,DeviceScenario.MemoryOnly);
        this.RunSmallRecoveryScenario(mode,DeviceScenario.StorageResident);
        this.RunSmallRecoveryScenario(mode,DeviceScenario.StorageEvicted);
    }

    [Theory]
    [MemberData(nameof(CheckpointModes))]
    public void Save_Load_StorageSegmentsReachConfiguredCapacity(CheckpointMode mode) {
        var root = TestPath.Directory<TsavoriteCheckpointCapacity>(mode.Name);
        try {
            var storeDir = Path.Combine(root,"store");
            var checkpointDir = Path.Combine(root,"checkpoint");
            Directory.CreateDirectory(storeDir);
            Directory.CreateDirectory(checkpointDir);
            var logPath = Path.Combine(storeDir,"log");
            var objPath = Path.Combine(storeDir,"obj");
            var token = SaveDictionaryLongString(
                logPath,
                objPath,
                checkpointDir,
                mode,
                SingleStoreIndexSize,
                SingleStoreSegmentSize,
                SingleStoreSegmentSize,
                SingleStoreCapacity,
                SingleStoreCapacity,
                preallocateFile: true,
                flushAndEvict: true,
                count: SmallCount);

            LoadDictionaryLongString(
                logPath,
                objPath,
                checkpointDir,
                token,
                mode,
                SingleStoreIndexSize,
                SingleStoreSegmentSize,
                SingleStoreSegmentSize,
                SingleStoreCapacity,
                SingleStoreCapacity,
                count: SmallCount);

            Assert.Equal(8,SingleStoreCapacity/SingleStoreSegmentSize);
            Assert.Equal(1,CountDeviceFiles(logPath));
        } finally {
            TryDelete(root);
        }
    }

    [Theory]
    [MemberData(nameof(CheckpointModes))]
    public void Save_Load_Stores1024_RestoresDataAndCreatesSegmentFiles(CheckpointMode mode) {
        var root = TestPath.Directory<TsavoriteCheckpointCapacity>(mode.Name);
        var descriptors = new StoreDescriptor[StoreCount];
        try {
            for(var i = 0;i<StoreCount;i++) {
                var descriptor = CreateDescriptor(root,i);
                descriptors[i]=descriptor;
                using var store = CreateAndPopulateStore(descriptor,i,mode);
            }

            Assert.Equal(2048,descriptors.Length*2);
            Assert.Equal(StoreCount,descriptors.Sum(static descriptor => CountDeviceFiles(descriptor.LogPath)+CountDeviceFiles(descriptor.ObjectLogPath)));

            for(var i = 0;i<StoreCount;i++) {
                using var handle = CreateRecoveryStore(descriptors[i],i);
                LoadStore(handle.Store,descriptors[i].Token,mode);
                AssertStoreData(handle.Store,i);
            }
        } finally {
            TryDelete(root);
        }
    }

    private void RunSmallRecoveryScenario(CheckpointMode mode,DeviceScenario scenario) {
        this.output.WriteLine($"Scenario={scenario},Mode={mode.Name}. MemoryOnlyDevice, storage device resident, and storage device evicted are checked explicitly.");
        var root = TestPath.Directory<TsavoriteCheckpointCapacity>(mode.Name,scenario.ToString());
        try {
            var checkpointDir = Path.Combine(root,"checkpoint");
            Directory.CreateDirectory(checkpointDir);
            switch(scenario) {
                case DeviceScenario.MemoryOnly: {
                    var logBuffer = new byte[MinSegmentSize*2];
                    var objBuffer = new byte[global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity];
                    var token = SaveDictionaryLongLongMemory(checkpointDir,logBuffer,objBuffer,mode,SmallCount);
                    LoadDictionaryLongLongMemory(checkpointDir,logBuffer,objBuffer,token,mode,SmallCount);
                    break;
                }
                case DeviceScenario.StorageResident:
                case DeviceScenario.StorageEvicted: {
                    var storeDir = Path.Combine(root,"store");
                    Directory.CreateDirectory(storeDir);
                    var logPath = Path.Combine(storeDir,"log");
                    var objPath = Path.Combine(storeDir,"obj");
                    var token = SaveDictionaryLongLong(
                        logPath,
                        objPath,
                        checkpointDir,
                        mode,
                        MinIndexSize,
                        MinPageSize,
                        MinSegmentSize,
                        MinMemorySize,
                        global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
                        preallocateFile: false,
                        flushAndEvict: scenario==DeviceScenario.StorageEvicted,
                        count: SmallCount);
                    LoadDictionaryLongLong(
                        logPath,
                        objPath,
                        checkpointDir,
                        token,
                        mode,
                        MinIndexSize,
                        MinPageSize,
                        MinSegmentSize,
                        MinMemorySize,
                        global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
                        count: SmallCount);
                    break;
                }
            }
        } finally {
            TryDelete(root);
        }
    }

    private static Guid SaveDictionaryLongLongMemory(string checkpointDir,byte[] logBuffer,byte[] objBuffer,CheckpointMode mode,int count) {
        using var logDevice = new MemoryOnlyDevice(MinSegmentSize*2,logBuffer);
        using var objDevice = new MemoryOnlyDevice(global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,objBuffer);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: MinIndexSize,
            PageSize: MinPageSize,
            SegmentSize: MinSegmentSize,
            MemorySize: MinMemorySize,
            CheckpointDir: checkpointDir,
            ObjectLogSegmentSize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = factory.Create<long,long>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
        using(var dict = store.Create()) {
            FillDictionaryLongLong(dict,count);
        }
        return SaveStore(store,mode);
    }

    private static void LoadDictionaryLongLongMemory(string checkpointDir,byte[] logBuffer,byte[] objBuffer,Guid token,CheckpointMode mode,int count) {
        using var logDevice = new MemoryOnlyDevice(MinSegmentSize*2,logBuffer);
        using var objDevice = new MemoryOnlyDevice(global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,objBuffer);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: MinIndexSize,
            PageSize: MinPageSize,
            SegmentSize: MinSegmentSize,
            MemorySize: MinMemorySize,
            CheckpointDir: checkpointDir,
            ObjectLogSegmentSize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = factory.Create<long,long>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
        LoadStore(store,token,mode);
        using var dict = store.Create();
        AssertDictionaryLongLong(dict,count);
    }

    private static Guid SaveDictionaryLongLong(
        string logPath,
        string objPath,
        string checkpointDir,
        CheckpointMode mode,
        long indexSize,
        long pageSize,
        long segmentSize,
        long memorySize,
        long capacity,
        bool preallocateFile,
        bool flushAndEvict,
        int count) {
        using var logDevice = CreateDevice(logPath,preallocateFile,capacity);
        using var objDevice = CreateDevice(objPath,preallocateFile,capacity);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: indexSize,
            PageSize: pageSize,
            SegmentSize: segmentSize,
            MemorySize: memorySize,
            CheckpointDir: checkpointDir,
            ObjectLogSegmentSize: segmentSize);
        using var store = factory.Create<long,long>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
        using(var dict = store.Create()) {
            FillDictionaryLongLong(dict,count);
        }
        if(flushAndEvict) {
            store.Store.Log.FlushAndEvict(wait: true);
        }
        return SaveStore(store,mode);
    }

    private static void LoadDictionaryLongLong(
        string logPath,
        string objPath,
        string checkpointDir,
        Guid token,
        CheckpointMode mode,
        long indexSize,
        long pageSize,
        long segmentSize,
        long memorySize,
        long capacity,
        int count) {
        using var logDevice = CreateDevice(logPath,preallocateFile: false,capacity);
        using var objDevice = CreateDevice(objPath,preallocateFile: false,capacity);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: indexSize,
            PageSize: pageSize,
            SegmentSize: segmentSize,
            MemorySize: memorySize,
            CheckpointDir: checkpointDir,
            ObjectLogSegmentSize: segmentSize);
        using var store = factory.Create<long,long>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
        LoadStore(store,token,mode);
        using var dict = store.Create();
        AssertDictionaryLongLong(dict,count);
    }

    private static Guid SaveDictionaryLongString(
        string logPath,
        string objPath,
        string checkpointDir,
        CheckpointMode mode,
        long indexSize,
        long pageSize,
        long segmentSize,
        long memorySize,
        long capacity,
        bool preallocateFile,
        bool flushAndEvict,
        int count) {
        using var logDevice = CreateDevice(logPath,preallocateFile,capacity);
        using var objDevice = CreateDevice(objPath,preallocateFile,capacity);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: indexSize,
            PageSize: pageSize,
            SegmentSize: segmentSize,
            MemorySize: memorySize,
            CheckpointDir: checkpointDir,
            ObjectLogSegmentSize: segmentSize);
        using var store = factory.Create<long,string>(logDevice,objDevice,new BlittableStrategy(),new MemoryPackSerializerStrategy());
        using(var dict = store.Create()) {
            FillDictionaryLongString(dict,count);
        }
        if(flushAndEvict) {
            store.Store.Log.FlushAndEvict(wait: true);
        }
        return SaveStore(store,mode);
    }

    private static void LoadDictionaryLongString(
        string logPath,
        string objPath,
        string checkpointDir,
        Guid token,
        CheckpointMode mode,
        long indexSize,
        long pageSize,
        long segmentSize,
        long memorySize,
        long capacity,
        int count) {
        using var logDevice = CreateDevice(logPath,preallocateFile: false,capacity);
        using var objDevice = CreateDevice(objPath,preallocateFile: false,capacity);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: indexSize,
            PageSize: pageSize,
            SegmentSize: segmentSize,
            MemorySize: memorySize,
            CheckpointDir: checkpointDir,
            ObjectLogSegmentSize: segmentSize);
        using var store = factory.Create<long,string>(logDevice,objDevice,new BlittableStrategy(),new MemoryPackSerializerStrategy());
        LoadStore(store,token,mode);
        using var dict = store.Create();
        AssertDictionaryLongString(dict,count);
    }

    private static StoreHandle CreateAndPopulateStore(StoreDescriptor descriptor,int index,CheckpointMode mode) {
        Directory.CreateDirectory(descriptor.StoreDir);
        Directory.CreateDirectory(descriptor.CheckpointDir);
        var logDevice = CreateDevice(descriptor.LogPath,preallocateFile: false,MinMemorySize);
        var objDevice = CreateDevice(descriptor.ObjectLogPath,preallocateFile: false,global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: MinIndexSize,
            PageSize: MinPageSize,
            SegmentSize: MinSegmentSize,
            MemorySize: MinMemorySize,
            CheckpointDir: descriptor.CheckpointDir,
            ObjectLogSegmentSize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        var store = CreateStore(factory,logDevice,objDevice,index);
        PopulateStore(store,index);
        descriptor.Token=SaveStore(store,mode);
        return new StoreHandle(store,logDevice,objDevice);
    }

    private static StoreHandle CreateRecoveryStore(StoreDescriptor descriptor,int index) {
        var logDevice = CreateDevice(descriptor.LogPath,preallocateFile: false,MinMemorySize);
        var objDevice = CreateDevice(descriptor.ObjectLogPath,preallocateFile: false,global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        var factory = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: MinIndexSize,
            PageSize: MinPageSize,
            SegmentSize: MinSegmentSize,
            MemorySize: MinMemorySize,
            CheckpointDir: descriptor.CheckpointDir,
            ObjectLogSegmentSize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        return new StoreHandle(CreateStore(factory,logDevice,objDevice,index),logDevice,objDevice);
    }

    private static CollectionStore CreateStore(global::Seamless.Collections.Tsavorite.Store factory,IDevice logDevice,IDevice objDevice,int index) =>
        (index%4) switch {
            0 => factory.Create<long,long>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy()),
            1 => factory.Create<long>(logDevice,objDevice,new BlittableStrategy()),
            2 => factory.Create<long,string>(logDevice,objDevice,new BlittableStrategy(),new MemoryPackSerializerStrategy()),
            _ => factory.Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy()),
        };

    private static void PopulateStore(CollectionStore store,int index) {
        switch(index%4) {
            case 0: {
                using var dict = ((DictionaryStore<long,long>)store).Create();
                dict.Add(index,index);
                break;
            }
            case 1: {
                using var set = ((HashSetStore<long>)store).Create();
                set.Add(index);
                break;
            }
            case 2: {
                using var dict = ((DictionaryStore<long,string>)store).Create();
                dict.Add(index,"value_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                break;
            }
            default: {
                using var set = ((HashSetStore<string>)store).Create();
                set.Add("value_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                break;
            }
        }
    }

    private static void AssertStoreData(CollectionStore store,int index) {
        switch(index%4) {
            case 0: {
                using var dict = ((DictionaryStore<long,long>)store).Create();
                Assert.True(dict.TryGetValue(index,out var value));
                Assert.Equal(index,value);
                break;
            }
            case 1: {
                using var set = ((HashSetStore<long>)store).Create();
                Assert.True(set.Contains(index));
                break;
            }
            case 2: {
                using var dict = ((DictionaryStore<long,string>)store).Create();
                Assert.True(dict.TryGetValue(index,out var value));
                Assert.Equal("value_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture),value);
                break;
            }
            default: {
                using var set = ((HashSetStore<string>)store).Create();
                Assert.True(set.Contains("value_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
                break;
            }
        }
    }

    private static void FillDictionaryLongLong(global::Seamless.Collections.Tsavorite.IDictionary<long,long> dict,int count) {
        for(var i = 0;i<count;i++) {
            dict.Add(i,i);
        }
    }

    private static void AssertDictionaryLongLong(global::Seamless.Collections.Tsavorite.IDictionary<long,long> dict,int count) {
        Assert.Equal(count,dict.Count);
        for(var i = 0;i<count;i++) {
            Assert.True(dict.TryGetValue(i,out var value),$"Key {i} was not found.");
            Assert.Equal(i,value);
        }
    }

    private static void FillDictionaryLongString(global::Seamless.Collections.Tsavorite.IDictionary<long,string> dict,int count) {
        for(var i = 0;i<count;i++) {
            dict.Add(i,"value_"+i.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static void AssertDictionaryLongString(global::Seamless.Collections.Tsavorite.IDictionary<long,string> dict,int count) {
        Assert.Equal(count,dict.Count);
        for(var i = 0;i<count;i++) {
            Assert.True(dict.TryGetValue(i,out var value),$"Key {i} was not found.");
            Assert.Equal("value_"+i.ToString(global::System.Globalization.CultureInfo.InvariantCulture),value);
        }
    }

    private static Guid SaveStore(CollectionStore store,CheckpointMode mode) =>
        mode.SaveKind==CheckpointSaveKind.Full
            ? ((dynamic)store).Save(mode.CheckpointType)
            : ((dynamic)store).SaveLogOnly(mode.CheckpointType);

    private static void LoadStore(CollectionStore store,Guid token,CheckpointMode mode) {
        _=mode;
        ((dynamic)store).Load(token);
    }

    private static IDevice CreateDevice(string path,bool preallocateFile,long capacity) =>
        global::Tsavorite.core.Devices.CreateLogDevice(path,preallocateFile: preallocateFile,deleteOnClose: false,capacity: capacity);

    private static int CountDeviceFiles(string path) {
        var directory = Path.GetDirectoryName(path);
        if(string.IsNullOrWhiteSpace(directory)||!Directory.Exists(directory)) {
            return 0;
        }
        var name = Path.GetFileName(path);
        return Directory.GetFiles(directory,name+"*",SearchOption.TopDirectoryOnly).Length;
    }

    private static StoreDescriptor CreateDescriptor(string root,int index) {
        var storeDir = Path.Combine(root,"store_"+index.ToString("D4",global::System.Globalization.CultureInfo.InvariantCulture));
        return new StoreDescriptor(
            storeDir,
            Path.Combine(storeDir,"log"),
            Path.Combine(storeDir,"obj"),
            Path.Combine(storeDir,"checkpoint"));
    }

    private static void TryDelete(string path) {
        try {
            if(Directory.Exists(path)) {
                Directory.Delete(path,recursive: true);
            }
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }

    public sealed record CheckpointMode(string Name,CheckpointSaveKind SaveKind,CheckpointType CheckpointType) {
        public override string ToString() => this.Name;
    }

    public enum CheckpointSaveKind {
        Full,
        LogOnly,
    }

    private enum DeviceScenario {
        MemoryOnly,
        StorageResident,
        StorageEvicted,
    }

    private sealed record StoreDescriptor(string StoreDir,string LogPath,string ObjectLogPath,string CheckpointDir) {
        public Guid Token { get; set; }
    }

    private sealed class StoreHandle(CollectionStore store,IDevice logDevice,IDevice objDevice):IDisposable {
        public CollectionStore Store { get; } = store;

        public void Dispose() {
            ((IDisposable)this.Store).Dispose();
            objDevice.Dispose();
            logDevice.Dispose();
        }
    }
}
