using Seamless.Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Seamless.Integrate;

[Collection("IntegrateTests")]
public sealed class Dictionary(ITestOutputHelper output) {
    private const int DataSize = 96;
    private readonly ITestOutputHelper output = output;

    [Fact]
    public void FlushAndEvict_managed() {
        FlushAndEvict(Managed, nameof(FlushAndEvict_managed));
    }

    [Fact]
    public void FlushAndEvict_unmanaged() {
        FlushAndEvict(Unmanaged, nameof(FlushAndEvict_unmanaged));
    }

    [Fact]
    public void Checkpoint_managed() {
        Checkpoint(Managed, nameof(Checkpoint_managed));
    }

    [Fact]
    public void Checkpoint_unmanaged() {
        Checkpoint(Unmanaged, nameof(Checkpoint_unmanaged));
    }

    private void FlushAndEvict<TKey, TValue>(DictionaryScenario<TKey, TValue> scenario, string methodName)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        var storeDir = TestDir(scenario, methodName);
        Directory.CreateDirectory(storeDir);

        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir, "log"), deleteOnClose: false);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir, "obj"), deleteOnClose: false);
            using var store = scenario.CreateStore(logDevice, objDevice, null, false, default);
            using var dict = store.Create();

            for(var i = 0; i < DataSize; i++) {
                dict.Add(scenario.MakeKey(i), scenario.MakeValue(i));
            }

            AssertContainsAll(dict, DataSize, scenario);
            store.Store.Log.FlushAndEvict(wait: true);
            dict.Refresh();

            AssertContainsAll(dict, DataSize, scenario);
            output.WriteLine(
                $"Verified {DataSize} entries across storage flush/evict. scenario={scenario.Name}"
            );
        } finally {
            try { Directory.Delete(storeDir, true); } catch { }
        }
    }

    private void Checkpoint<TKey, TValue>(DictionaryScenario<TKey, TValue> scenario, string methodName)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        var storeDir = TestDir(scenario, methodName);
        var checkpointDir = Path.Combine(storeDir, "checkpoint");
        Directory.CreateDirectory(storeDir);
        Directory.CreateDirectory(checkpointDir);

        Guid token;
        using(var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir, "log"), deleteOnClose: false))
        using(var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir, "obj"), deleteOnClose: false))
        using(var store = scenario.CreateStore(logDevice, objDevice, checkpointDir, false, default))
        using(var dict = store.Create()) {
            for(var i = 0; i < DataSize; i++) {
                dict.Add(scenario.MakeKey(i), scenario.MakeValue(i));
            }

            store.Store.Log.FlushAndEvict(wait: true);
            token = store.Save();
        }

        using(var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir, "log"), deleteOnClose: false))
        using(var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(storeDir, "obj"), deleteOnClose: false))
        using(var store = scenario.CreateStore(logDevice, objDevice, checkpointDir, true, token))
        {
            store.Load(token);
            using var dict = store.Create();
            AssertContainsAll(dict, DataSize, scenario);
        }

        output.WriteLine(
            $"Verified {DataSize} entries after checkpoint restore. scenario={scenario.Name}"
        );
        try { Directory.Delete(storeDir, true); } catch { }
    }

    private void AssertContainsAll<TKey, TValue>(global::Seamless.Tsavorite.Collections.IDictionary<TKey, TValue> dict, int count, DictionaryScenario<TKey, TValue> scenario)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        Assert.Equal(count, dict.Count);
        for(var i = 0; i < count; i++) {
            var key = scenario.MakeKey(i);
            Assert.True(dict.TryGetValue(key, out var value), $"Key {key} was not found.");
            Assert.Equal(scenario.MakeValue(i), value!);
        }
    }

    private string TestDir<TKey, TValue>(DictionaryScenario<TKey, TValue> scenario, string methodName) {
        return TestPath.Directory(scenario.Name, methodName);
    }

    private static readonly DictionaryScenario<string, string> Managed = new DictionaryScenario<string, string>(
        "Test.Seamless.Integrate.Dictionary.Managed",
        static i => $"key_{i:D6}",
        static i => $"value_{i:D6}_{new string('x', 100)}",
        static (logDevice, objDevice, checkpointDir, recovery, token) => {
            var factory = Store.Create(CheckpointDir: checkpointDir);
            if(recovery) {
                factory = factory.WithRecoveryIndexSize(token, 64);
            }

            return factory.Create<string, string>(logDevice, objDevice, new MemoryPackSerializerStrategy(), new MemoryPackSerializerStrategy());
        }
    );

    private static readonly DictionaryScenario<int, int> Unmanaged = new DictionaryScenario<int, int>(
        "Test.Seamless.Integrate.Dictionary.Unmanaged",
        static i => i,
        static i => i * 100,
        static (logDevice, objDevice, checkpointDir, recovery, token) => {
            var factory = Store.Create(CheckpointDir: checkpointDir);
            if(recovery) {
                factory = factory.WithRecoveryIndexSize(token, 64);
            }

            return factory.Create<int, int>(logDevice, objDevice, new BlittableStrategy(), new BlittableStrategy());
        }
    );
}

public sealed record DictionaryScenario<TKey, TValue>(
    string Name,
    Func<int, TKey> MakeKey,
    Func<int, TValue> MakeValue,
    Func<IDevice, IDevice, string?, bool, Guid, DictionaryStore<TKey, TValue>> CreateStore
);
