using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Stress;

/// <summary>
/// シングルスレッドの決定論的 Add/Save/Load/Contains/Remove 連続実行テスト。
/// 同じシードで毎回同じハッシュ結果になることを確認する。
/// </summary>
[Trait("Category","Stress")]
public class SerialRandomStressTests {
    private const int Seed = 20260417;
    private const double RemoveRate = 0.5;

    private static string CreateTempDir(string tag) {
        var dir = Path.Combine(Path.GetTempPath(),"Seamless_Stress_"+tag+"_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
    private static void TryCleanup(string dir) {
        try { if(Directory.Exists(dir)) Directory.Delete(dir,recursive: true); }
        catch { /* best-effort */ }
    }

    // ──────────── UnmanagedUnmanagedDictionary<int,int> ────────────
    [Theory, InlineData(10_000), InlineData(100_000)]
    public void UnmanagedIntDictionary_AddEvictSaveLoadRemove_Deterministic(int count) {
        var temp = CreateTempDir("uintdict");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            Guid token;
            // Phase 1: Add + FlushAndEvict + Save
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<int,int>(logDevice,objDevice);
                using var dict = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    dict.Add(keys[i],keys[i]*2);
                store.Store.Log.FlushAndEvict(wait: true);
                token=store.Save();
            }
            // Phase 2: Load + Contains + Remove
            string addHash, removeHash;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<int,int>(logDevice,objDevice);
                store.Load(token);
                using var dict = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    Assert.True(dict.ContainsKey(keys[i]),$"Missing key {keys[i]}");
                addHash=StressGolden.HashInt32PairsByProbe(keys,key => {
                    var found = dict.TryGetValue(key,out var value);
                    return (found,value);
                });
                for(var i = 0; i<keys.Length; i++) {
                    if(removeMask[i])
                        Assert.True(dict.Remove(keys[i]));
                }
                removeHash=StressGolden.HashInt32PairsByProbe(keys,key => {
                    var found = dict.TryGetValue(key,out var value);
                    return (found,value);
                });
            }
            StressGolden.AssertGolden($"SerialRandom_UintDict_Add_{count}",addHash);
            StressGolden.AssertGolden($"SerialRandom_UintDict_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    // ──────────── ManagedManagedDictionary<string,string> ────────────
    [Theory, InlineData(10_000), InlineData(100_000)]
    public void ManagedStringDictionary_AddEvictSaveLoadRemove_Deterministic(int count) {
        var temp = CreateTempDir("strdict");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var rawKeys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keys = rawKeys.Select(k => "k"+k).ToArray();
            var values = rawKeys.Select(k => "v"+k).ToArray();
            Guid token;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<string,string>(logDevice,objDevice,new ManagedTag(),new ManagedTag());
                using var dict = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    dict.Add(keys[i],values[i]);
                store.Store.Log.FlushAndEvict(wait: true);
                token=store.Save();
            }
            string addHash, removeHash;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<string,string>(logDevice,objDevice,new ManagedTag(),new ManagedTag());
                store.Load(token);
                using var dict = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    Assert.True(dict.ContainsKey(keys[i]),$"Missing key {keys[i]}");
                addHash=StressGolden.HashStringPairsByProbe(keys,key => {
                    var found = dict.TryGetValue(key,out var value);
                    return (found,value??string.Empty);
                });
                for(var i = 0; i<keys.Length; i++) {
                    if(removeMask[i])
                        Assert.True(dict.Remove(keys[i]));
                }
                removeHash=StressGolden.HashStringPairsByProbe(keys,key => {
                    var found = dict.TryGetValue(key,out var value);
                    return (found,value??string.Empty);
                });
            }
            StressGolden.AssertGolden($"SerialRandom_StrDict_Add_{count}",addHash);
            StressGolden.AssertGolden($"SerialRandom_StrDict_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    // ──────────── UnmanagedHashSet<int> ────────────
    [Theory, InlineData(10_000), InlineData(100_000)]
    public void UnmanagedIntHashSet_AddEvictSaveLoadRemove_Deterministic(int count) {
        var temp = CreateTempDir("uintset");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            Guid token;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<int>(logDevice,objDevice);
                using var set = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    Assert.True(set.Add(keys[i]));
                store.Store.Log.FlushAndEvict(wait: true);
                token=store.Save();
            }
            string addHash, removeHash;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<int>(logDevice,objDevice);
                store.Load(token);
                using var set = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    Assert.True(set.Contains(keys[i]),$"Missing key {keys[i]}");
                addHash=StressGolden.HashInt32KeysByProbe(keys,key => set.Contains(key));
                for(var i = 0; i<keys.Length; i++) {
                    if(removeMask[i])
                        Assert.True(set.Remove(keys[i]));
                }
                removeHash=StressGolden.HashInt32KeysByProbe(keys,key => set.Contains(key));
            }
            StressGolden.AssertGolden($"SerialRandom_UintSet_Add_{count}",addHash);
            StressGolden.AssertGolden($"SerialRandom_UintSet_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    // ──────────── ManagedHashSet<string> ────────────
    [Theory, InlineData(10_000), InlineData(100_000)]
    public void ManagedStringHashSet_AddEvictSaveLoadRemove_Deterministic(int count) {
#if TSAVORITE1_1_1
        return;
#endif
        var temp = CreateTempDir("strset");
        try {
            var checkpointDir = Path.Combine(temp,"cp");
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var rawKeys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMask = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keys = rawKeys.Select(k => "s"+k).ToArray();
            Guid token;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<string>(logDevice,objDevice,new ManagedTag());
                using var set = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    Assert.True(set.Add(keys[i]));
                store.Store.Log.FlushAndEvict(wait: true);
                token=store.Save();
            }
            string addHash, removeHash;
            {
                using var logDevice = Devices.CreateLogDevice(logPath);
                using var objDevice = Devices.CreateLogDevice(objPath);
                using var store = Factory.Create(CheckpointDir: checkpointDir)
                    .Create<string>(logDevice,objDevice,new ManagedTag());
                store.Load(token);
                using var set = store.Create();
                for(var i = 0; i<keys.Length; i++)
                    Assert.True(set.Contains(keys[i]),$"Missing key {keys[i]}");
                addHash=StressGolden.HashStringKeysByProbe(keys,key => set.Contains(key));
                for(var i = 0; i<keys.Length; i++) {
                    if(removeMask[i])
                        Assert.True(set.Remove(keys[i]));
                }
                removeHash=StressGolden.HashStringKeysByProbe(keys,key => set.Contains(key));
            }
            StressGolden.AssertGolden($"SerialRandom_StrSet_Add_{count}",addHash);
            StressGolden.AssertGolden($"SerialRandom_StrSet_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }
}
