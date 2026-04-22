using System.Diagnostics;

namespace Test.Seamless.Benchmarks;

internal static class BenchmarkRunSettings {
    private const string FullMatrixEnvironmentVariable = "SEAMLESS_BENCHMARK_FULL_MATRIX";

    public static bool RunFullMatrix { get; } = ParseEnabled(Environment.GetEnvironmentVariable(FullMatrixEnvironmentVariable));
    public static IReadOnlyList<int> ParallelValues { get; } = RunFullMatrix ? new[] { 1,2,4,8 } : new[] { 1 };
    public static IReadOnlyList<int> PairParallelValues { get; } = RunFullMatrix ? new[] { 1,2,4,8 } : new[] { 1 };
    public static IReadOnlyList<double> MutableFractions { get; } = RunFullMatrix ? new[] { 0.0,0.5,1.0 } : new[] { 0.5 };
    public static IReadOnlyList<long> IndexSizes { get; } = RunFullMatrix ? new[] {
        1L<<6,
        1L<<7,
        1L<<8,
        1L<<9,
        1L<<10,
        1L<<11,
        1L<<12,
        1L<<13,
        1L<<14,
        1L<<15,
        1L<<16,
    } : new[] { 1L<<10 };
    public static int Count { get; } = RunFullMatrix ? 1<<14 : 1<<8;

    private static bool ParseEnabled(string? value) {
        if(string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("1",StringComparison.OrdinalIgnoreCase)
            || value.Equals("true",StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes",StringComparison.OrdinalIgnoreCase)
            || value.Equals("on",StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// ベンチマーク全体の回数スケール。
/// <c>分子</c>/<c>分母</c> を書き換えるだけで全ベンチマークの反復回数を一斉に調整できる。
/// 既定 分子=100, 分母=100 で ScaleCount(Count)==Count。
/// </summary>
public static class BenchmarkScale {
    private const long 分子 = 100;
    private const long 分母 = 100;
    public static long ScaleCount(long Count) => 分子*Count/分母;
}

/// <summary>
/// 各ベンチマーククラスの共通土台。
/// </summary>
/// <remarks>
/// <para>通常テスト実行では 10 分超えを避けるため smoke 用の 1 点だけ回し、
/// 環境変数 <c>SEAMLESS_BENCHMARK_FULL_MATRIX=1</c> を与えたときだけ full matrix を回す。</para>
/// <para><c>Count</c> は各メソッドの計測部分が 10 秒程度で終わる 2 冪の要素数を一括設定する。
/// smoke 実行では件数を落として通常スイートを短時間で終わらせる。</para>
/// <para><c>IndexSize</c> は full matrix では <c>1L&lt;&lt;6</c> から <c>1L&lt;&lt;16</c> まで変化させる。
/// 反復回数はテスト側で <see cref="Scaled"/> を使用する。</para>
/// <para>Parallel パラメータは 1,2,4,8,16 のいずれかで、スレッド毎に
/// <c>Store.Create</c> で独立した Store+コレクションを 1 つずつ生成して動作させる。</para>
/// </remarks>
[Collection("Benchmark")]
public abstract class Benchmark(ITestOutputHelper output){
    /// <summary>スケール適用済みの反復回数。</summary>
    protected static long Scaled => BenchmarkScale.ScaleCount(BenchmarkRunSettings.Count);
    protected static IReadOnlyList<long> IndexSizes => BenchmarkRunSettings.IndexSizes;
    protected static IReadOnlyList<int> ParallelValues => BenchmarkRunSettings.ParallelValues;
    protected static IReadOnlyList<int> PairParallelValues => BenchmarkRunSettings.PairParallelValues;
    protected static IReadOnlyList<double> MutableFractions => BenchmarkRunSettings.MutableFractions;

    /// <summary>(Parallel, MutableFraction, IndexSize) の全組み合わせ。</summary>
    public static TheoryData<int,double,long> Matrix {
        get {
            var data = new TheoryData<int,double,long>();
            foreach(var p in ParallelValues)
                foreach(var mf in MutableFractions)
                    foreach(var indexSize in IndexSizes)
                        data.Add(p,mf,indexSize);
            return data;
        }
    }
    private void Write(string line) => output.WriteLine(line);

    /// <summary>
    /// 初期化 (<paramref name="setup"/>)→バリア→計測 (<paramref name="measured"/>) を
    /// <paramref name="parallel"/> スレッドで並走させ、計測部分の最大経過時間を出力する。
    /// </summary>
    protected void RunMeasured(
        int parallel,
        double mutableFraction,
        long indexSize,
        long opCount,
        Action<int> setup,
        Action<int> measured,
        string label
    ) {
        var barrier = new Barrier(parallel);
        long maxMs = 0;
        var sync = new object();
        System.Exception? err = null;
        var threads = new Thread[parallel];
        for(var i = 0; i<parallel; i++) {
            var id = i;
            threads[i]=new Thread(() => {
                try {
                    setup(id);
                    barrier.SignalAndWait();
                    var local = Stopwatch.StartNew();
                    measured(id);
                    local.Stop();
                    lock(sync) { if(local.ElapsedMilliseconds>maxMs) maxMs=local.ElapsedMilliseconds; }
                }
                catch(System.Exception e) {
                    lock(sync) { err??=e; }
                    try { barrier.RemoveParticipant(); } catch { }
                }
            });
            threads[i].Start();
        }
        foreach(var t in threads) t.Join();
        if(err is not null) throw err;
        this.Write($"[{this.GetType().Name}.{label}] P={parallel} MF={mutableFraction} IS={indexSize} N={opCount}: {maxMs}ms");
    }
}
