// 同一クエリを System.Linq / Seamless(FLinq) / ZLinq で直接比較するベンチマーク群
//
// ─ ライブラリ特性 ──────────────────────────────────────────────────────────────
//  System.Linq : 標準。IEnumerable ベース。各 operator でヒープ割り当てが発生。
//  Seamless    : System.Linq と同一構文のまま、SourceGenerator がコンパイル時に
//                struct enumerator チェーンへ変換（allocate フリー）。
//                呼び出し元コードを変えない = ライブラリへの依存度ゼロ（疎結合）。
//  ZLinq       : AsValueEnumerable() で明示的にオプトイン。struct enumerator。
//                参照実装として比較の基準に使用。
//
// ─ 疎結合の実現方法 ────────────────────────────────────────────────────────────
//  ・System.Linq は通常の LINQ 構文そのまま（変更ゼロ）
//  ・Seamless は同じ LINQ 構文のまま、SourceGenerator が自動で struct 展開
//    → アプリコードには一切変更が不要 = ライブラリへの結合度が最も低い
//  ・ZLinq は AsValueEnumerable() という明示的な呼び出しが必要
//    → ライブラリへの依存が構文に表れる（より密な結合）
//
// ─ FLinq の private メソッドパターン ──────────────────────────────────────────
//  SourceGenerator は「System.Linq 呼び出しを含むメソッドへの呼び出し箇所」を
//  インターセプトして展開する。private ヘルパーメソッドに切り出すことで
//  「このメソッドが SG の展開対象」と明確に表現できる。
//    [Benchmark] public int[] FLinq() => FLinqImpl();     ← BDN 計測対象
//    private int[] FLinqImpl() => _data.Where(...)...;    ← SG がインターセプト・展開
//
// ─ static フィールドにする理由 ─────────────────────────────────────────────────
//  SG は private static フィールドを展開コード内で直接参照できる。
//  instance フィールドの場合、展開された static インターセプターメソッドから
//  アクセスできないためコンパイルエラーになる。
//  WhereSelectStringJoin.cs と同様に static readonly で定義する。
//  （N 変化ごとに [GlobalSetup] が static フィールドを書き換える）

using ZLinq;

namespace Benchmark.Comparison;

// ────────────────────────────────────────────────────────────────────────────────
// Q1: Where → ToArray  （フィルタリング + 実体化）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereToArray
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int[] SystemLinq() => _data.Where(static x => x % 2 == 0).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int[] FLinq() => FLinqImpl();
    private static int[] FLinqImpl() => _data.Where(static x => x % 2 == 0).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int[] ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).ToArray();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q2: Select → ToArray  （変換 + 実体化）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SelectToArray
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int[] SystemLinq() => _data.Select(static x => x * 2).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int[] FLinq() => FLinqImpl();
    private static int[] FLinqImpl() => _data.Select(static x => x * 2).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int[] ZLinq() => _data.AsValueEnumerable().Select(static x => x * 2).ToArray();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q3: Where → Select → ToArray  （フィルタ + 変換 + 実体化）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereSelectToArray
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int[] SystemLinq() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int[] FLinq() => FLinqImpl();
    private static int[] FLinqImpl() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int[] ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).Select(static x => x * 2).ToArray();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q4: Where → Sum  （フィルタ + 数値集計）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereSum
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int SystemLinq() => _data.Where(static x => x % 2 == 0).Sum();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int FLinq() => FLinqImpl();
    private static int FLinqImpl() => _data.Where(static x => x % 2 == 0).Sum();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).Sum();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q5: Where → Count  （フィルタ + 件数カウント）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereCount
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int SystemLinq() => _data.Where(static x => x % 2 == 0).Count();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int FLinq() => FLinqImpl();
    private static int FLinqImpl() => _data.Where(static x => x % 2 == 0).Count();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).Count();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q6: Where → Select → Sum  （フィルタ + 変換 + 集計）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereSelectSum
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int SystemLinq() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).Sum();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int FLinq() => FLinqImpl();
    private static int FLinqImpl() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).Sum();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).Select(static x => x * 2).Sum();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q7: OrderBy → ToArray  （ソート）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderByToArray
{
    private static int[] _data = null!;
    [Params(1_000, 100_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).Reverse().ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int[] SystemLinq() => _data.OrderBy(static x => x).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int[] FLinq() => FLinqImpl();
    private static int[] FLinqImpl() => _data.OrderBy(static x => x).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int[] ZLinq() => _data.AsValueEnumerable().OrderBy(static x => x).ToArray();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q8: Where → First  （フィルタ + 先頭要素取得）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereFirst
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int SystemLinq() => _data.Where(static x => x % 2 == 0).First();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int FLinq() => FLinqImpl();
    private static int FLinqImpl() => _data.Where(static x => x % 2 == 0).First();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).First();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q9: Where → Select → OrderBy → ToArray  （複合チェーン）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereSelectOrderByToArray
{
    private static int[] _data = null!;
    [Params(1_000, 50_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).Reverse().ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public int[] SystemLinq() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).OrderBy(static x => x).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public int[] FLinq() => FLinqImpl();
    private static int[] FLinqImpl() => _data.Where(static x => x % 2 == 0).Select(static x => x * 2).OrderBy(static x => x).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int[] ZLinq() => _data.AsValueEnumerable().Where(static x => x % 2 == 0).Select(static x => x * 2).OrderBy(static x => x).ToArray();
}

// ────────────────────────────────────────────────────────────────────────────────
// Q10: Select → Sum  （変換 + 集計。純粋な演算速度比較）
// ────────────────────────────────────────────────────────────────────────────────
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SelectSum
{
    private static int[] _data = null!;
    [Params(1_000, 100_000, 1_000_000)] public int N { get; set; }
    [GlobalSetup] public void Setup() => _data = Enumerable.Range(0, N).ToArray();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public long SystemLinq() => _data.Select(static x => (long)x * x).Sum();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public long FLinq() => FLinqImpl();
    private static long FLinqImpl() => _data.Select(static x => (long)x * x).Sum();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public long ZLinq() => _data.AsValueEnumerable().Select(static x => (long)x * x).Sum();
}
