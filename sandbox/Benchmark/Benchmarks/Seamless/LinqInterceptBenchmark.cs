// Benchmark: System.Linq (baseline) vs Seamless-intercepted LINQ
// When built with the Seamless.SourceGenerator Analyzer, LINQ calls in these methods are
// replaced with inlined struct-based code. Use "SystemLinq" config to compare both.
//
// Element counts are initial estimates for ~100ms per iteration.
// Run with --list and adjust N values per method as needed.
namespace Benchmark.Seamless;

// ── Aggregation ───────────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class SumBenchmark
{
    private int[] _data = null!;
    [Params(10_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public int ManualLoop() { int s = 0; foreach (var x in _data) s += x; return s; }
    [Benchmark] public int Sum() => _data.Sum();
    [Benchmark] public int WhereSum() => _data.Where(static x => x % 2 == 0).Sum();
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class CountBenchmark
{
    private int[] _data = null!;
    [Params(10_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public int ManualLoop() { int c = 0; foreach (var _ in _data) c++; return c; }
    [Benchmark] public int Count() => _data.Count();
    [Benchmark] public int WhereCount() => _data.Where(static x => x % 2 == 0).Count();
    [Benchmark] public int CountPredicate() => _data.Count(static x => x % 2 == 0);
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class AnyAllBenchmark
{
    private int[] _data = null!;
    [Params(5_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public bool ManualAny() { foreach (var x in _data) if (x == N - 1) return true; return false; }
    [Benchmark] public bool Any() => _data.Any(x => x == N - 1);
    [Benchmark] public bool All() => _data.All(static x => x >= 0);
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class ContainsBenchmark
{
    private int[] _data = null!;
    [Params(5_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public bool ManualLoop() { foreach (var x in _data) if (x == N - 1) return true; return false; }
    [Benchmark] public bool Contains() => _data.Contains(N - 1);
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class MinMaxBenchmark
{
    private int[] _data = null!;
    [Params(10_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public int ManualMin() { int m = int.MaxValue; foreach (var x in _data) if (x < m) m = x; return m; }
    [Benchmark] public int Min() => _data.Min();
    [Benchmark] public int Max() => _data.Max();
    [Benchmark] public int WhereMin() => _data.Where(static x => x % 2 == 0).Min();
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class AverageBenchmark
{
    private int[] _data = null!;
    [Params(10_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public double ManualLoop() { long s = 0; foreach (var x in _data) s += x; return (double)s / _data.Length; }
    [Benchmark] public double Average() => _data.Average();
    [Benchmark] public double WhereAverage() => _data.Where(static x => x % 2 == 0).Average();
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class AggregateBenchmark
{
    private int[] _data = null!;
    [Params(5_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(1, N).ToArray();

    [Benchmark(Baseline = true)] public int ManualLoop() { int acc = 0; foreach (var x in _data) acc += x; return acc; }
    [Benchmark] public int Aggregate() => _data.Aggregate(0, static (acc, x) => acc + x);
}

// ── Element access ────────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class FirstLastBenchmark
{
    private int[] _data = null!;
    [Params(1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public int ManualFirst() => _data[0];
    [Benchmark] public int First() => _data.First();
    [Benchmark] public int FirstOrDefault() => _data.FirstOrDefault();
    [Benchmark] public int Last() => _data.Last();
    [Benchmark] public int LastOrDefault() => _data.LastOrDefault();
    [Benchmark] public int First_Predicate() => _data.First(static x => x > 999_000);
    [Benchmark] public int FirstOrDefault_Predicate() => _data.FirstOrDefault(static x => x > 999_000);
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class ElementAtBenchmark
{
    private int[] _data = null!;
    [Params(1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)] public int DirectAccess() => _data[N / 2];
    [Benchmark] public int ElementAt() => _data.ElementAt(N / 2);
    [Benchmark] public int ElementAtOrDefault() => _data.ElementAtOrDefault(N / 2);
}

// ── Transformation ────────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class WhereBenchmark
{
    private int[] _data = null!;
    [Params(5_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    public int ManualLoop() { int s = 0; foreach (var x in _data) if (x % 2 == 0) s++; return s; }
    [Benchmark] public int WhereCount() => _data.Where(static x => x % 2 == 0).Count();
    [Benchmark] public int WhereSum() => _data.Where(static x => x % 2 == 0).Sum();
    [Benchmark] public List<int> WhereToList() => _data.Where(static x => x % 2 == 0).ToList();
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class SelectBenchmark
{
    private int[] _data = null!;
    [Params(5_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    public int ManualLoop() { int s = 0; foreach (var x in _data) s += x * 2; return s; }
    [Benchmark] public int SelectSum() => _data.Select(static x => x * 2).Sum();
    [Benchmark] public int[] SelectToArray() => _data.Select(static x => x * 2).ToArray();
    [Benchmark] public List<int> SelectToList() => _data.Select(static x => x * 2).ToList();
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class WhereSelectBenchmark
{
    private int[] _data = null!;
    [Params(3_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    public int ManualLoop() { int s = 0; foreach (var x in _data) if (x % 2 == 0) s += x * 2; return s; }
    [Benchmark] public int WhereSelectSum() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).Sum();
    [Benchmark] public int WhereSelectCount() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).Count();
}

// ── Materialization ───────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class ToArrayBenchmark
{
    private int[] _data = null!;
    [Params(1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    public int[] ManualLoop() { var r = new int[_data.Length]; for (int i = 0; i < _data.Length; i++) r[i] = _data[i]; return r; }
    [Benchmark] public int[] ToArray() => _data.Select(static x => x).ToArray();
    [Benchmark] public int[] WhereToArray() => _data.Where(static x => x % 2 == 0).ToArray();
}

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class ToListBenchmark
{
    private int[] _data = null!;
    [Params(1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    public List<int> ManualLoop() { var r = new List<int>(_data.Length); foreach (var x in _data) r.Add(x); return r; }
    [Benchmark] public List<int> ToList() => _data.Select(static x => x).ToList();
    [Benchmark] public List<int> WhereToList() => _data.Where(static x => x % 2 == 0).ToList();
}

// ── Ordering ──────────────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class OrderByBenchmark
{
    private int[] _data = null!;
    [Params(500_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).Reverse().ToArray();

    [Benchmark(Baseline = true)] public int[] ArraySort() { var r = (int[])_data.Clone(); Array.Sort(r); return r; }
    [Benchmark] public int[] OrderBy() => _data.OrderBy(static x => x).ToArray();
    [Benchmark] public int[] OrderByDescending() => _data.OrderByDescending(static x => x).ToArray();
    [Benchmark] public int[] Order() => _data.Order().ToArray();
}

// ── Partitioning ──────────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class SkipTakeBenchmark
{
    private int[] _data = null!;
    [Params(3_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    public int ManualLoop() { int s = 0; for (int i = N / 2; i < _data.Length; i++) s += _data[i]; return s; }
    [Benchmark] public int SkipSum() => _data.Skip(N / 2).Sum();
    [Benchmark] public int TakeSum() => _data.Take(N / 2).Sum();
    [Benchmark] public int SkipTakeSum() => _data.Skip(N / 4).Take(N / 2).Sum();
}

// ── Set operations ────────────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class DistinctBenchmark
{
    private int[] _data = null!;
    [Params(500_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() { int divisor = N / 10; _data = Enumerable.Range(0, N).Select(x => x % divisor).ToArray(); }

    [Benchmark(Baseline = true)]
    public int ManualLoop() { var s = new HashSet<int>(); foreach (var x in _data) s.Add(x); return s.Count; }
    [Benchmark] public int[] Distinct() => _data.Distinct().ToArray();
    [Benchmark] public int DistinctCount() => _data.Distinct().Count();
}

// ── Sequence operations ───────────────────────────────────────────────────────

[MemoryDiagnoser, SimpleJob(warmupCount: 1, iterationCount: 3)]
public class SequenceEqualBenchmark
{
    private int[] _a = null!;
    private int[] _b = null!;
    [Params(5_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() { _a = Enumerable.Range(0, N).ToArray(); _b = Enumerable.Range(0, N).ToArray(); }

    [Benchmark(Baseline = true)]
    public bool ManualLoop() { for (int i = 0; i < _a.Length; i++) if (_a[i] != _b[i]) return false; return true; }
    [Benchmark] public bool SequenceEqual() => _a.SequenceEqual(_b);
}
