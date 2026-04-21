using System.Collections;
using System.Diagnostics;
using System.Numerics;
using Seamless.Collections;
using Seamless.Stores;

namespace Seamless.BenchmarkCalibration;

/// <summary>
/// Benchmark の <c>const int Count</c> を実測キャリブレーションする Console ツール。
/// baseline (Parallel=1, MutableFraction=1.0, Unmanaged int,int / int) で各操作を
/// 倍々に Count を増やしながら実行し、計測部分が <see cref="TargetElapsedMs"/> を超える最小の Count を出力する。
/// </summary>
internal static class Program
{
    private const long TargetElapsedMs = 1000;
    private const int MinCount = 1_000;
    private const int MaxCount = 50_000_000;

    private static void Main()
    {
        Console.WriteLine($"# Seamless Benchmark Count calibration — baseline Parallel=1, MutableFraction=1.0, Unmanaged");
        Console.WriteLine($"# Target per-method wall time: {TargetElapsedMs}ms");
        Console.WriteLine();

        // Dictionary<int,int> (Unmanaged) baseline
        CalibrateDict("Add", isFreshDict: true, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) d.Add(i, i * 2 + 1); });
        CalibrateDict("Remove_key", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) d.Remove(i); });
        CalibrateDict("Remove_key_value", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) d.Remove(i, out _); });
        CalibrateDict("Clear", isFreshDict: false, measureOpsEqualsCount: false,
            measure: (d, n) => d.Clear());
        CalibrateDict("TryGetValue", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) d.TryGetValue(i, out _); });
        CalibrateDict("Contains", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var c = (ICollection<KeyValuePair<int, int>>)d; for (var i = 0; i < n; i++) _ = c.Contains(new(i, i * 2 + 1)); });
        CalibrateDict("ContainsKey", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) _ = d.ContainsKey(i); });
        CalibrateDict("foreach", isFreshDict: false, measureOpsEqualsCount: false,
            measure: (d, n) => { foreach (var kv in d) { } });
        CalibrateDict("Count", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) _ = d.Count; });
        CalibrateDict("Keys", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) _ = d.Keys; });
        CalibrateDict("Values", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) _ = d.Values; });
        CalibrateDict("ToList", isFreshDict: false, measureOpsEqualsCount: false,
            measure: (d, n) => { _ = System.Linq.Enumerable.ToList(d); });
        CalibrateDict("get_Item", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) _ = d[i]; });
        CalibrateDict("set_Item", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { for (var i = 0; i < n; i++) d[i] = i * 3; });
        CalibrateDict("IDictionary.Add", isFreshDict: true, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IDictionary<int, int>)d; for (var i = 0; i < n; i++) x.Add(i, i * 2 + 1); });
        CalibrateDict("IDictionary.ContainsKey", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.ContainsKey(i); });
        CalibrateDict("IDictionary.Remove", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.Remove(i); });
        CalibrateDict("IDictionary.TryGetValue", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IDictionary<int, int>)d; for (var i = 0; i < n; i++) x.TryGetValue(i, out _); });
        CalibrateDict("IDictionary.Keys", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.Keys; });
        CalibrateDict("IDictionary.Values", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.Values; });
        CalibrateDict("ICollection.Add", isFreshDict: true, measureOpsEqualsCount: true,
            measure: (d, n) => { var c = (ICollection<KeyValuePair<int, int>>)d; for (var i = 0; i < n; i++) c.Add(new(i, i * 2 + 1)); });
        CalibrateDict("ICollection.Clear", isFreshDict: false, measureOpsEqualsCount: false,
            measure: (d, n) => { var c = (ICollection<KeyValuePair<int, int>>)d; c.Clear(); });
        CalibrateDict("ICollection.Contains", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var c = (ICollection<KeyValuePair<int, int>>)d; for (var i = 0; i < n; i++) _ = c.Contains(new(i, i * 2 + 1)); });
        CalibrateDict("ICollection.CopyTo", isFreshDict: false, measureOpsEqualsCount: false,
            measure: (d, n) => { var c = (ICollection<KeyValuePair<int, int>>)d; var a = new KeyValuePair<int, int>[c.Count]; c.CopyTo(a, 0); });
        CalibrateDict("ICollection.Remove", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var c = (ICollection<KeyValuePair<int, int>>)d; for (var i = 0; i < n; i++) _ = c.Remove(new(i, i * 2 + 1)); });
        CalibrateDict("IReadOnlyCollection.Count", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var c = (IReadOnlyCollection<KeyValuePair<int, int>>)d; for (var i = 0; i < n; i++) _ = c.Count; });
        CalibrateDict("IReadOnlyDictionary.get_Item", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IReadOnlyDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x[i]; });
        CalibrateDict("IReadOnlyDictionary.ContainsKey", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IReadOnlyDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.ContainsKey(i); });
        CalibrateDict("IReadOnlyDictionary.TryGetValue", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IReadOnlyDictionary<int, int>)d; for (var i = 0; i < n; i++) x.TryGetValue(i, out _); });
        CalibrateDict("IReadOnlyDictionary.Keys", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IReadOnlyDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.Keys; });
        CalibrateDict("IReadOnlyDictionary.Values", isFreshDict: false, measureOpsEqualsCount: true,
            measure: (d, n) => { var x = (IReadOnlyDictionary<int, int>)d; for (var i = 0; i < n; i++) _ = x.Values; });
        CalibrateDict("IEnumerable.GetEnumerator", isFreshDict: false, measureOpsEqualsCount: false,
            measure: (d, n) => { var e = ((IEnumerable)d).GetEnumerator(); try { while (e.MoveNext()) { } } finally { if (e is IDisposable x) x.Dispose(); } });

        // HashSet<int> (Unmanaged) baseline
        CalibrateSet("Add", isFreshSet: true, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { for (var i = 0; i < n; i++) s.Add(i); });
        CalibrateSet("Remove", isFreshSet: false, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { for (var i = 0; i < n; i++) s.Remove(i); });
        CalibrateSet("Contains", isFreshSet: false, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { for (var i = 0; i < n; i++) _ = s.Contains(i); });
        CalibrateSet("CopyTo", isFreshSet: false, needsOther: false, measureOpsEqualsCount: false,
            measure: (s, o, n) => { var a = new int[s.Count]; s.CopyTo(a, 0); });
        CalibrateSet("foreach", isFreshSet: false, needsOther: false, measureOpsEqualsCount: false,
            measure: (s, o, n) => { foreach (var v in s) { } });
        CalibrateSet("Count", isFreshSet: false, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { for (var i = 0; i < n; i++) _ = s.Count; });
        CalibrateSet("Clear", isFreshSet: false, needsOther: false, measureOpsEqualsCount: false,
            measure: (s, o, n) => s.Clear());
        CalibrateSet("IntersectWith", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => s.IntersectWith(o!));
        CalibrateSet("SymmetricExceptWith", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => s.SymmetricExceptWith(o!));
        CalibrateSet("UnionWith", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => s.UnionWith(o!));
        CalibrateSet("ExceptWith", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => s.ExceptWith(o!));
        CalibrateSet("IsProperSubsetOf", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => { _ = s.IsProperSubsetOf(o!); });
        CalibrateSet("IsProperSupersetOf", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => { _ = s.IsProperSupersetOf(o!); });
        CalibrateSet("IsSubsetOf", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => { _ = s.IsSubsetOf(o!); });
        CalibrateSet("IsSupersetOf", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => { _ = s.IsSupersetOf(o!); });
        CalibrateSet("Overlaps", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => { _ = s.Overlaps(o!); });
        CalibrateSet("SetEquals", isFreshSet: false, needsOther: true, measureOpsEqualsCount: false,
            measure: (s, o, n) => { _ = s.SetEquals(o!); });
        CalibrateSet("ICollection.Add", isFreshSet: true, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { var c = (ICollection<int>)s; for (var i = 0; i < n; i++) c.Add(i); });
        CalibrateSet("ICollection.Clear", isFreshSet: false, needsOther: false, measureOpsEqualsCount: false,
            measure: (s, o, n) => { var c = (ICollection<int>)s; c.Clear(); });
        CalibrateSet("ICollection.Contains", isFreshSet: false, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { var c = (ICollection<int>)s; for (var i = 0; i < n; i++) _ = c.Contains(i); });
        CalibrateSet("ICollection.CopyTo", isFreshSet: false, needsOther: false, measureOpsEqualsCount: false,
            measure: (s, o, n) => { var c = (ICollection<int>)s; var a = new int[c.Count]; c.CopyTo(a, 0); });
        CalibrateSet("ICollection.Remove", isFreshSet: false, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { var c = (ICollection<int>)s; for (var i = 0; i < n; i++) _ = c.Remove(i); });
        CalibrateSet("IReadOnlyCollection.Count", isFreshSet: false, needsOther: false, measureOpsEqualsCount: true,
            measure: (s, o, n) => { var c = (IReadOnlyCollection<int>)s; for (var i = 0; i < n; i++) _ = c.Count; });
        CalibrateSet("IEnumerable.GetEnumerator", isFreshSet: false, needsOther: false, measureOpsEqualsCount: false,
            measure: (s, o, n) => { var e = ((IEnumerable)s).GetEnumerator(); try { while (e.MoveNext()) { } } finally { if (e is IDisposable x) x.Dispose(); } });

        Console.WriteLine();
        Console.WriteLine("# Done.");
    }

    // ────────────────── Dictionary calibration driver ──────────────────

    private static void CalibrateDict(
        string name,
        bool isFreshDict,
        bool measureOpsEqualsCount,
        Action<ISeamlessDictionary<int, int>, int> measure
    )
    {
        var count = MinCount;
        long lastElapsed = 0;
        while (count <= MaxCount)
        {
            var indexSize = (long)BitOperations.RoundUpToPowerOf2((uint)count);
            var memorySize = Math.Max(Seamless.Const.LogMemorySize << 11, count * 64L);
            using var store = Factory.Create(IndexSize: indexSize, MemorySize: memorySize, MutableFraction: 1.0).Create<int, int>();
            using var dict = store.Create();
            if (!isFreshDict)
            {
                for (var i = 0; i < count; i++) dict[i] = i * 2 + 1;
            }
            var sw = Stopwatch.StartNew();
            measure(dict, count);
            sw.Stop();
            lastElapsed = sw.ElapsedMilliseconds;
            if (lastElapsed >= TargetElapsedMs) break;
            count = Math.Min(count * 2, MaxCount);
            if (count == MaxCount && lastElapsed < TargetElapsedMs) break;
        }
        Console.WriteLine($"Dictionary.{name,-38} Count={count,10:N0}  elapsed={lastElapsed,6}ms");
    }

    // ────────────────── HashSet calibration driver ──────────────────

    private static void CalibrateSet(
        string name,
        bool isFreshSet,
        bool needsOther,
        bool measureOpsEqualsCount,
        Action<ISeamlessHashSet<int>, ISeamlessHashSet<int>?, int> measure
    )
    {
        var count = MinCount;
        long lastElapsed = 0;
        while (count <= MaxCount)
        {
            var indexSize = (long)BitOperations.RoundUpToPowerOf2((uint)count);
            var memorySize = Math.Max(Seamless.Const.LogMemorySize << 11, count * 64L);
            var factory = Factory.Create(IndexSize: indexSize, MemorySize: memorySize, MutableFraction: 1.0);
            using var storeA = factory.Create<int>(new UnmanagedTag());
            using var setA = storeA.Create();
            if (!isFreshSet) for (var i = 0; i < count; i++) setA.Add(i);
            ISeamlessHashSet<int>? setB = null;
            IDisposable? storeB = null;
            if (needsOther)
            {
                var sB = factory.Create<int>(new UnmanagedTag());
                storeB = sB;
                setB = sB.Create();
                for (var i = 0; i < count; i++) setB.Add(i + count / 2);
            }
            try
            {
                var sw = Stopwatch.StartNew();
                measure(setA, setB, count);
                sw.Stop();
                lastElapsed = sw.ElapsedMilliseconds;
            }
            finally
            {
                setB?.Dispose();
                storeB?.Dispose();
            }
            if (lastElapsed >= TargetElapsedMs) break;
            count = Math.Min(count * 2, MaxCount);
            if (count == MaxCount && lastElapsed < TargetElapsedMs) break;
        }
        Console.WriteLine($"HashSet.{name,-41} Count={count,10:N0}  elapsed={lastElapsed,6}ms");
    }
}
