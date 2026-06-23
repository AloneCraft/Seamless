using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Seamless.Integrate;

public abstract class HashSetStorageBoundaryTests<TKey>(ITestOutputHelper output)
    where TKey : IEquatable<TKey> {
    private const int DataSize = 96;
    private readonly ITestOutputHelper output = output;

    protected abstract string TestClassName { get; }
    protected abstract TKey MakeKey(int i);
    protected abstract HashSetStore<TKey> CreateStore(IDevice logDevice,IDevice objDevice,string? checkpointDir = null,bool recovery = false,Guid token = default);

    private string TestDir(string methodName) =>
        TestPath.Directory(this.TestClassName,methodName);

    [Fact]
    public void FlushAndEvict後も検索が正しい() {
        var storeDir = this.TestDir(nameof(this.FlushAndEvict後も検索が正しい));
        Directory.CreateDirectory(storeDir);

        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir,"log"),deleteOnClose: false);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir,"obj"),deleteOnClose: false);
            using var store = this.CreateStore(logDevice,objDevice);
            using var set = store.Create();

            for(var i = 0; i<DataSize; i++) {
                Assert.True(set.Add(this.MakeKey(i)));
            }

            this.AssertContainsAll(set,DataSize);
            store.Store.Log.FlushAndEvict(wait: true);
            set.Refresh();

            this.AssertContainsAll(set,DataSize);
            this.output.WriteLine($"Verified {DataSize} hash set keys across storage flush/evict.");
        }
        finally {
            try { Directory.Delete(storeDir,true); } catch { }
        }
    }

    [Fact]
    public void Checkpoint復元後も検索が正しい() {
        var storeDir = this.TestDir(nameof(this.Checkpoint復元後も検索が正しい));
        var checkpointDir = Path.Combine(storeDir,"checkpoint");
        Directory.CreateDirectory(storeDir);
        Directory.CreateDirectory(checkpointDir);

        Guid token;
        using(var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir,"log"),deleteOnClose: false))
        using(var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir,"obj"),deleteOnClose: false))
        using(var store = this.CreateStore(logDevice,objDevice,checkpointDir))
        using(var set = store.Create()) {
            for(var i = 0; i<DataSize; i++) {
                set.Add(this.MakeKey(i));
            }

            store.Store.Log.FlushAndEvict(wait: true);
            token=store.Save();
        }

        using(var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir,"log"),deleteOnClose: false))
        using(var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir,"obj"),deleteOnClose: false))
        using(var store = this.CreateStore(logDevice,objDevice,checkpointDir,recovery: true,token: token))
        {
            store.Load(token);
            using var set = store.Create();
            this.AssertContainsAll(set,DataSize);
        }

        this.output.WriteLine($"Verified {DataSize} hash set keys after checkpoint restore.");
        try { Directory.Delete(storeDir,true); } catch { }
    }

    private void AssertContainsAll(IHashSet<TKey> set,int count) {
        Assert.Equal(count,set.Count);
        for(var i = 0; i<count; i++) {
            Assert.True(set.Contains(this.MakeKey(i)),$"Key {this.MakeKey(i)} was not found.");
        }
    }

}

[Collection("IntegrateTests")]
public sealed class HashSet(ITestOutputHelper output):HashSetStorageBoundaryTests<string>(output) {
    protected override string TestClassName => "Test.Seamless.Integrate.HashSet";
    protected override string MakeKey(int i) => $"hashset_key_{i:D6}_"+new string('a',50);

    protected override HashSetStore<string> CreateStore(IDevice logDevice,IDevice objDevice,string? checkpointDir = null,bool recovery = false,Guid token = default) {
        var factory = Store.Create(CheckpointDir: checkpointDir);

        if(recovery) {
            factory=factory.WithRecoveryIndexSize(token,64);
        }

        return factory.Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy());
    }
}

