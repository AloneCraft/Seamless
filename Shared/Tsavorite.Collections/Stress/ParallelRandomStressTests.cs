using Tsavorite.core;
namespace Tsavorite.Collections.Stress;

/// <summary>
/// マルチスレッド Stress テスト。
/// 1 つの Store を共有し、スレッドごとに store.Create() で独立したコレクションハンドル
/// (独立 ClientSession) を取得する。各スレッドは互いに素なキー分割で操作することで
/// 最終状態の決定論を保証する。
/// </summary>
public class ParallelRandomStressTests {
    private const int Seed = 20260417;
    private const double RemoveRate = 0.5;
    private static readonly int ThreadCount = Math.Max(2,Math.Min(8,Environment.ProcessorCount));
    private const int StressCountSmall = 256;
    private const int StressCountLarge = 1024;

    private static string CreateTempDir(string tag) {
        var dir = TestPath.Directory<ParallelRandomStressTests>(tag);
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
        for(var t = 0; t<threads; t++) buckets[t]=[];
        for(var i = 0; i<keys.Length; i++)
            buckets[((uint)keys[i].GetHashCode())%(uint)threads].Add(keys[i]);
        var result = new int[threads][];
        for(var t = 0; t<threads; t++) result[t]= [.. buckets[t]];
        return result;
    }

    [Theory, InlineData(StressCountSmall), InlineData(StressCountLarge)]
    public void UnmanagedIntDictionary_ParallelAddRemove_Deterministic(int count) {
        const int Count = 1;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"uintdict-{count}");
        try {
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMaskFlat = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keyToRemove = new Dictionary<int,bool>(count);
            for(var i = 0; i<keys.Length; i++) keyToRemove[keys[i]]=removeMaskFlat[i];
            var partitions = Partition(keys,ThreadCount);
            var expected = keys.ToDictionary(key => key,key => key*2);
            var expectedAfterRemove = new Dictionary<int,int>(expected);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMaskFlat[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Store.Create().Create<int,int>(logDevice,objDevice)) {
                for(var r = 0; r<repeat; r++) {
                    using(var dict = store.Create()) {
                        dict.Clear();
                    }
                    Parallel.For(0,ThreadCount,t => {
                        using var dict = store.Create();
                        foreach(var k in partitions[t])
                            dict.Add(k,k*2);
                    });
                    // 全体ハッシュは単一ハンドル経由で列挙して得る。
                    using(var dict = store.Create()) {
                        StressReferenceAssert.DictionaryEquals(keys,expected,key => {
                            var found = dict.TryGetValue(key,out var value);
                            return (found,value);
                        });
                    }
                    Parallel.For(0,ThreadCount,t => {
                        using var dict = store.Create();
                        foreach(var k in partitions[t]) {
                            if(keyToRemove[k])
                                dict.Remove(k);
                        }
                    });
                    using(var dict = store.Create()) {
                        StressReferenceAssert.DictionaryEquals(keys,expectedAfterRemove,key => {
                            var found = dict.TryGetValue(key,out var value);
                            return (found,value);
                        });
                    }
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(StressCountSmall)]
    public void ManagedStringDictionary_ParallelAddRemove_Deterministic(int count) {
        const int Count = 5;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"strdict-{count}");
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
            var expected = new Dictionary<string,string>(count,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++)
                expected[keys[i]]=values[i];
            var expectedAfterRemove = new Dictionary<string,string>(expected,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMaskFlat[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Store.Create().Create<string,string>(logDevice,objDevice,new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy())) {
                for(var r = 0; r<repeat; r++) {
                    using(var dict = store.Create()) {
                        dict.Clear();
                    }
                    Parallel.For(0,ThreadCount,t => {
                        using var dict = store.Create();
                        foreach(var k in partitions[t])
                            dict.Add(k,keyToValue[k]);
                    });
                    using(var dict = store.Create()) {
                        StressReferenceAssert.DictionaryEquals(keys,expected,key => {
                            var found = dict.TryGetValue(key,out var value);
                            return (found,value??string.Empty);
                        });
                    }
                    Parallel.For(0,ThreadCount,t => {
                        using var dict = store.Create();
                        foreach(var k in partitions[t])
                            if(keyToRemove[k])
                                dict.Remove(k);
                    });
                    using(var dict = store.Create()) {
                        StressReferenceAssert.DictionaryEquals(keys,expectedAfterRemove,key => {
                            var found = dict.TryGetValue(key,out var value);
                            return (found,value??string.Empty);
                        });
                    }
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(StressCountSmall), InlineData(StressCountLarge)]
    public void UnmanagedIntHashSet_ParallelAddRemove_Deterministic(int count) {
        const int Count = 1;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"uintset-{count}");
        try {
            var logPath = Path.Combine(temp,"log");
            var objPath = Path.Combine(temp,"obj");
            var keys = StressScript.SampleKeys(Seed,count*4,count);
            var removeMaskFlat = StressScript.RemoveMask(Seed^0x5a5a,count,RemoveRate);
            var keyToRemove = new Dictionary<int,bool>(count);
            for(var i = 0; i<keys.Length; i++) keyToRemove[keys[i]]=removeMaskFlat[i];
            var partitions = Partition(keys,ThreadCount);
            var expected = new global::System.Collections.Generic.HashSet<int>(keys);
            var expectedAfterRemove = new global::System.Collections.Generic.HashSet<int>(expected);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMaskFlat[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Store.Create().Create<int>(logDevice,objDevice)) {
                for(var r = 0; r<repeat; r++) {
                    using(var set = store.Create()) {
                        set.Clear();
                    }
                    Parallel.For(0,ThreadCount,t => {
                        using var set = store.Create();
                        foreach(var k in partitions[t]) set.Add(k);
                    });
                    using(var set = store.Create()) StressReferenceAssert.SetEquals(keys,expected,key => set.Contains(key));
                    Parallel.For(0,ThreadCount,t => {
                        using var set = store.Create();
                        foreach(var k in partitions[t])
                            if(keyToRemove[k]) set.Remove(k);
                    });
                    using(var set = store.Create()) StressReferenceAssert.SetEquals(keys,expectedAfterRemove,key => set.Contains(key));
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(StressCountSmall)]
    public void ManagedStringHashSet_ParallelAddRemove_Deterministic(int count) {
        const int Count = 5;
        var repeat = StressScale.Apply(Count);
        var temp = CreateTempDir($"strset-{count}");
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
            var expected = new global::System.Collections.Generic.HashSet<string>(keys,StringComparer.Ordinal);
            var expectedAfterRemove = new global::System.Collections.Generic.HashSet<string>(expected,StringComparer.Ordinal);
            for(var i = 0; i<keys.Length; i++) {
                if(removeMaskFlat[i])
                    expectedAfterRemove.Remove(keys[i]);
            }
            using(var logDevice = Devices.CreateLogDevice(logPath))
            using(var objDevice = Devices.CreateLogDevice(objPath))
            using(var store = Store.Create().Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy())) {
                for(var r = 0; r<repeat; r++) {
                    using(var set = store.Create()) {
                        set.Clear();
                    }
                    Parallel.For(0,ThreadCount,t => {
                        using var set = store.Create();
                        foreach(var k in partitions[t]) set.Add(k);
                    });
                    using(var set = store.Create()) StressReferenceAssert.SetEquals(keys,expected,key => set.Contains(key));
                    Parallel.For(0,ThreadCount,t => {
                        using var set = store.Create();
                        foreach(var k in partitions[t])
                            if(keyToRemove[k]) set.Remove(k);
                    });
                    using(var set = store.Create()) StressReferenceAssert.SetEquals(keys,expectedAfterRemove,key => set.Contains(key));
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }
}