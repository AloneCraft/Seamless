using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Stress;

/// <summary>
/// マルチスレッド Stress テスト。
/// 1 つの Store を共有し、スレッドごとに store.Create() で独立したコレクションハンドル
/// (独立 ClientSession) を取得する。各スレッドは互いに素なキー分割で操作することで
/// 最終状態の決定論を保証する。
/// </summary>
[Trait("Category","Stress")]
public class ParallelRandomStressTests {
    private const int Seed = 20260417;
    private const double RemoveRate = 0.5;
    private static readonly int ThreadCount = Math.Max(2,Math.Min(8,Environment.ProcessorCount));

    private static string CreateTempDir(string tag) {
        var dir = Path.Combine(Path.GetTempPath(),"Seamless_PStress_"+tag+"_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
    private static void TryCleanup(string dir) {
        try { if(Directory.Exists(dir)) Directory.Delete(dir,recursive: true); }
        catch { }
    }

    /// <summary>キーをスレッド数で mod 分割する。</summary>
    private static int[][] Partition(int[] keys,int threads) {
        var buckets = new List<int>[threads];
        for(var t = 0; t<threads; t++) buckets[t]=new List<int>();
        for(var i = 0; i<keys.Length; i++)
            buckets[((uint)keys[i].GetHashCode())%(uint)threads].Add(keys[i]);
        var result = new int[threads][];
        for(var t = 0; t<threads; t++) result[t]= [.. buckets[t]];
        return result;
    }

    [Theory, InlineData(10_000), InlineData(100_000)]
    public void UnmanagedIntDictionary_ParallelAddRemove_Deterministic(int count) {
        var temp = CreateTempDir("uintdict");
        try {
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMaskFlat = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keyToRemove = new Dictionary<int,bool>(count);
            for(var i = 0; i<keys.Length; i++) keyToRemove[keys[i]]=removeMaskFlat[i];
            var partitions = Partition(keys,ThreadCount);
            string addHash, removeHash;
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Factory.Create().Create<int,int>(logDevice,objDevice)) {
                Parallel.For(0,ThreadCount,t => {
                    using var dict = store.Create();
                    foreach(var k in partitions[t])
                        dict.Add(k,k*2);
                });
                // 全体ハッシュは単一ハンドル経由で列挙して得る。
                using(var dict = store.Create()) {
                    addHash=StressGolden.HashInt32Pairs(dict);
                }
                Parallel.For(0,ThreadCount,t => {
                    using var dict = store.Create();
                    foreach(var k in partitions[t]) {
                        if(keyToRemove[k])
                            dict.Remove(k);
                    }
                });
                using(var dict = store.Create()) {
                    removeHash=StressGolden.HashInt32Pairs(dict);
                }
            }
            StressGolden.AssertGolden($"ParallelRandom_UintDict_Add_{count}",addHash);
            StressGolden.AssertGolden($"ParallelRandom_UintDict_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(10_000)]
    public void ManagedStringDictionary_ParallelAddRemove_Deterministic(int count) {
        var temp = CreateTempDir("strdict");
        try {
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var rawKeys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMaskFlat = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keys = rawKeys.Select(k => "k"+k).ToArray();
            var values = rawKeys.Select(k => "v"+k).ToArray();
            var keyToRemove = new Dictionary<string,bool>(count,StringComparer.Ordinal);
            var keyToValue = new Dictionary<string,string>(count,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++) {
                keyToRemove[keys[i]]=removeMaskFlat[i];
                keyToValue[keys[i]]=values[i];
            }
            var partitions = Partition([.. Enumerable.Range(0,keys.Length)],ThreadCount)
                .Select(idxs => idxs.Select(i => keys[i]).ToArray()).ToArray();
            string addHash, removeHash;
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Factory.Create().Create<string,string>(logDevice,objDevice,new ManagedTag(),new ManagedTag())) {
                Parallel.For(0,ThreadCount,t => {
                    using var dict = store.Create();
                    foreach(var k in partitions[t])
                        dict.Add(k,keyToValue[k]);
                });
                using(var dict = store.Create()) {
                    addHash=StressGolden.HashStringPairs(dict);
                }
                Parallel.For(0,ThreadCount,t => {
                    using var dict = store.Create();
                    foreach(var k in partitions[t])
                        if(keyToRemove[k])
                            dict.Remove(k);
                });
                using(var dict = store.Create()) {
                    removeHash=StressGolden.HashStringPairs(dict);
                }
            }
            StressGolden.AssertGolden($"ParallelRandom_StrDict_Add_{count}",addHash);
            StressGolden.AssertGolden($"ParallelRandom_StrDict_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(10_000), InlineData(100_000)]
    public void UnmanagedIntHashSet_ParallelAddRemove_Deterministic(int count) {
        var temp = CreateTempDir("uintset");
        try {
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMaskFlat = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keyToRemove = new Dictionary<int,bool>(count);
            for(var i = 0; i<keys.Length; i++) keyToRemove[keys[i]]=removeMaskFlat[i];
            var partitions = Partition(keys,ThreadCount);
            string addHash, removeHash;
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Factory.Create().Create<int>(logDevice,objDevice)) {
                Parallel.For(0,ThreadCount,t => {
                    using var set = store.Create();
                    foreach(var k in partitions[t]) set.Add(k);
                });
                using(var set = store.Create()) addHash=StressGolden.HashInt32Keys(set);
                Parallel.For(0,ThreadCount,t => {
                    using var set = store.Create();
                    foreach(var k in partitions[t])
                        if(keyToRemove[k]) set.Remove(k);
                });
                using(var set = store.Create()) removeHash=StressGolden.HashInt32Keys(set);
            }
            StressGolden.AssertGolden($"ParallelRandom_UintSet_Add_{count}",addHash);
            StressGolden.AssertGolden($"ParallelRandom_UintSet_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(10_000)]
    public void ManagedStringHashSet_ParallelAddRemove_Deterministic(int count) {
        var temp = CreateTempDir("strset");
        try {
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var rawKeys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMaskFlat = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keys = rawKeys.Select(k => "s"+k).ToArray();
            var keyToRemove = new Dictionary<string,bool>(count,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++) keyToRemove[keys[i]]=removeMaskFlat[i];
            var partitions = Partition([.. Enumerable.Range(0,keys.Length)],ThreadCount)
                .Select(idxs => idxs.Select(i => keys[i]).ToArray()).ToArray();
            string addHash, removeHash;
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Factory.Create().Create<string>(logDevice,objDevice,new ManagedTag())) {
                Parallel.For(0,ThreadCount,t => {
                    using var set = store.Create();
                    foreach(var k in partitions[t]) set.Add(k);
                });
                using(var set = store.Create()) addHash=StressGolden.HashStringKeys(set);
                Parallel.For(0,ThreadCount,t => {
                    using var set = store.Create();
                    foreach(var k in partitions[t])
                        if(keyToRemove[k]) set.Remove(k);
                });
                using(var set = store.Create()) removeHash=StressGolden.HashStringKeys(set);
            }
            StressGolden.AssertGolden($"ParallelRandom_StrSet_Add_{count}",addHash);
            StressGolden.AssertGolden($"ParallelRandom_StrSet_Remove_{count}",removeHash);
        }
        finally {
            TryCleanup(temp);
        }
    }
}
