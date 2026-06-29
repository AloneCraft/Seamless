using Seamless.Tsavorite.Stores;
using System.Linq;
using Tsavorite.core;
using Seamless.Collections;
namespace Tsavorite.Collection.Stress;

/// <summary>
/// シングルスレチE��の決定論的 Add/Save/Load/Contains/Remove 連続実行テスト、E/// 同じシードで毎回同じハッシュ結果になることを確認する、E/// </summary>
public class SerialRandomStressTests {
    private const int Seed = 20260417;
    private const double RemoveRate = 0.5;
    private const int StressCountSmall = 256;
    private const int StressCountLarge = 1024;

    private static string CreateTempDir(string tag) {
        var dir = TestPath.Directory<SerialRandomStressTests>(tag);
        Directory.CreateDirectory(dir);
        return dir;
    }
    private static void TryCleanup(string dir) {
        try { if(Directory.Exists(dir)) Directory.Delete(dir,recursive: true); }
        catch { /* best-effort */ }
    }

    // ──────────── Dictionary<int,int> ────────────
    [Theory, InlineData(StressCountSmall), InlineData(StressCountLarge)]
    public void UnmanagedIntDictionary_AddEvictSaveLoadRemove_Deterministic(int count) {
        const int Count = 1;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"uintdict-{count}");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var expected = keys.ToDictionary(key => key,key => key*2);
            var expectedAfterRemove = new Dictionary<int,int>(expected);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMask[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            for(var r = 0; r<repeat; r++) {
                Guid token;
                // Measured section: deterministic add/save/load/remove flow.
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<int,int>(logDevice,objDevice);
                    using var dict = store.Create();
                    foreach(var t in keys)
                        dict.Add(t,t*2);
                    store.Store.Log.FlushAndEvict(wait: true);
                    token=store.Save();
                }
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<int,int>(logDevice,objDevice);
                    store.Load(token);
                    using var dict = store.Create();
                    foreach(var t in keys)
                        Assert.True(dict.ContainsKey(t),$"Missing key {t}");
                    StressReferenceAssert.DictionaryEquals(keys,expected,key => {
                        var found = dict.TryGetValue(key,out var value);
                        return (found,value);
                    });
                    for(var i = 0; i<keys.Length; i++) {
                        if(removeMask[i])
                            Assert.True(dict.Remove(keys[i]));
                    }
                    StressReferenceAssert.DictionaryEquals(keys,expectedAfterRemove,key => {
                        var found = dict.TryGetValue(key,out var value);
                        return (found,value);
                    });
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    // ──────────── Dictionary<string,string> ────────────
    [Theory, InlineData(StressCountSmall), InlineData(StressCountLarge)]
    public void ManagedStringDictionary_AddEvictSaveLoadRemove_Deterministic(int count) {
        const int Count = 3;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"strdict-{count}");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var rawKeys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keys = rawKeys.Select(k => "k"+k).ToArray();
            var values = rawKeys.Select(k => "v"+k).ToArray();
            var expected = new Dictionary<string,string>(count,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++)
                expected[keys[i]]=values[i];
            var expectedAfterRemove = new Dictionary<string,string>(expected,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMask[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            for(var r = 0; r<repeat; r++) {
                Guid token;
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<string,string>(logDevice,objDevice,new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
                    using var dict = store.Create();
                    for(var i = 0; i<keys.Length; i++)
                        dict.Add(keys[i],values[i]);
                    store.Store.Log.FlushAndEvict(wait: true);
                    token=store.Save();
                }
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<string,string>(logDevice,objDevice,new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
                    store.Load(token);
                    using var dict = store.Create();
                    foreach(var t in keys)
                        Assert.True(dict.ContainsKey(t),$"Missing key {t}");
                    StressReferenceAssert.DictionaryEquals(keys,expected,key => {
                        var found = dict.TryGetValue(key,out var value);
                        return (found,value??string.Empty);
                    });
                    for(var i = 0; i<keys.Length; i++) {
                        if(removeMask[i])
                            Assert.True(dict.Remove(keys[i]));
                    }
                    StressReferenceAssert.DictionaryEquals(keys,expectedAfterRemove,key => {
                        var found = dict.TryGetValue(key,out var value);
                        return (found,value??string.Empty);
                    });
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    // ──────────── HashSet<int> ────────────
    [Theory, InlineData(StressCountSmall), InlineData(StressCountLarge)]
    public void UnmanagedIntHashSet_AddEvictSaveLoadRemove_Deterministic(int count) {
        const int Count = 1;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"uintset-{count}");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var expected = new global::System.Collections.Generic.HashSet<int>(keys);
            var expectedAfterRemove = new global::System.Collections.Generic.HashSet<int>(expected);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMask[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            for(var r = 0; r<repeat; r++) {
                Guid token;
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<int>(logDevice,objDevice);
                    using var set = store.Create();
                    foreach(var t in keys)
                        Assert.True(set.Add(t));
                    store.Store.Log.FlushAndEvict(wait: true);
                    token=store.Save();
                }
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<int>(logDevice,objDevice);
                    store.Load(token);
                    using var set = store.Create();
                    foreach(var t in keys)
                        Assert.True(set.Contains(t),$"Missing key {t}");
                    StressReferenceAssert.SetEquals(keys,expected,set.Contains);
                    for(var i = 0; i<keys.Length; i++) {
                        if(removeMask[i])
                            Assert.True(set.Remove(keys[i]));
                    }
                    StressReferenceAssert.SetEquals(keys,expectedAfterRemove,set.Contains);
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    // ──────────── HashSet<string> ────────────
    [Theory, InlineData(StressCountSmall), InlineData(StressCountLarge)]
    public void ManagedStringHashSet_AddEvictSaveLoadRemove_Deterministic(int count) {
        const int Count = 5;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"strset-{count}");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var rawKeys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keys = rawKeys.Select(k => "s"+k).ToArray();
            var expected = new global::System.Collections.Generic.HashSet<string>(keys,StringComparer.Ordinal);
            var expectedAfterRemove = new global::System.Collections.Generic.HashSet<string>(expected,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMask[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            for(var r = 0; r<repeat; r++) {
                Guid token;
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy());
                    using var set = store.Create();
                    for(var i = 0; i<keys.Length; i++)
                        Assert.True(set.Add(keys[i]));
                    store.Store.Log.FlushAndEvict(wait: true);
                    token=store.Save();
                }
                {
                    using var logDevice = Devices.CreateLogDevice(logPath);
                    using var objDevice = Devices.CreateLogDevice(objPath);
                    using var store = Store.Create(CheckpointDir: checkpointDir)
                        .Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy());
                    store.Load(token);
                    using var set = store.Create();
                    for(var i = 0; i<keys.Length; i++)
                        Assert.True(set.Contains(keys[i]),$"Missing key {keys[i]}");
                    StressReferenceAssert.SetEquals(keys,expected,key => set.Contains(key));
                    for(var i = 0; i<keys.Length; i++) {
                        if(removeMask[i])
                            Assert.True(set.Remove(keys[i]));
                    }
                    StressReferenceAssert.SetEquals(keys,expectedAfterRemove,key => set.Contains(key));
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }
}
