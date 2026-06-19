using System.Diagnostics;

namespace Test.Benchmark.Tsavorite.Collections;

internal static class BenchmarkRunSettings {
    private const string FullMatrixEnvironmentVariable = "Tsavorite_BENCHMARK_FULL_MATRIX";
    public const int DefaultDataCount = 100;
    public const int FullMatrixDataCount = 100;

    public static bool RunFullMatrix { get; } = ParseEnabled(Environment.GetEnvironmentVariable(FullMatrixEnvironmentVariable));
    public static int DataCount { get; } = RunFullMatrix ? FullMatrixDataCount : DefaultDataCount;

    private static bool ParseEnabled(string? value) {
        if(string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("1",StringComparison.OrdinalIgnoreCase)
            ||value.Equals("true",StringComparison.OrdinalIgnoreCase)
            ||value.Equals("yes",StringComparison.OrdinalIgnoreCase)
            ||value.Equals("on",StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 各ベンチマーククラスの共通土台。
/// </summary>
/// <remarks>
/// <para>通常テスト実行では 10 分超えを避けるため smoke 用の 1 点だけ回し、
/// 環境変数 <c>Tsavorite_BENCHMARK_FULL_MATRIX=1</c> を与えたときだけ full matrix を回す。</para>
/// <para>投入件数は <see cref="BenchmarkRunSettings.DefaultDataCount"/> を既定とし、
/// full matrix でも同じ件数を維持して通常スイートを短時間に保つ。</para>
/// <para>Parallel パラメータは matrix 行に直接定義し、スレッド毎に
/// <c>Store.Create</c> で独立した Store+コレクションを 1 つずつ生成して動作させる。</para>
/// </remarks>
[Collection("Benchmark")]
public abstract class Benchmark(ITestOutputHelper output) {
    /// <summary>スケール適用済みの反復回数。</summary>
    protected static long Scaled => BenchmarkRunSettings.DataCount;

    /// <summary>(Parallel, MutableFraction, IndexSize, SegmentSize, MemorySizeBits, PageSizeBits) の組み合わせ。</summary>
    public static TheoryData<int,double,long,long,int,int> Matrix {
        get {
            var data = new TheoryData<int,double,long,long,int,int>();
            data.Add(1,0.5,64,1L<<12,14,12);
            if(BenchmarkRunSettings.RunFullMatrix) {
                data.Add(1,0.0,64,1L<<12,14,12);
                data.Add(1,1.0,64,1L<<12,14,12);
                data.Add(4,0.5,64,1L<<12,14,12);
                data.Add(1,0.5,1L<<10,1L<<12,14,12);
                data.Add(1,0.5,1L<<10,1L<<20,16,12);
            }
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
        long segmentSize,
        int memorySizeBits,
        int pageSizeBits,
        long opCount,
        Action<int> setup,
        Action<int> measured,
        string label
    ) {
        var barrier = new Barrier(parallel);
        long maxMs = 0;
        var sync = new object();
        Exception? err = null;
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
                catch(Exception e) {
                    lock(sync) { err??=e; }
                    try { barrier.RemoveParticipant(); } catch { }
                }
            });
            threads[i].Start();
        }
        foreach(var t in threads) t.Join();
        if(err is not null) throw err;
        this.Write($"[{this.GetType().Name}.{label}] P={parallel} MF={mutableFraction} IS={indexSize} SS={segmentSize} MSb={memorySizeBits} PSb={pageSizeBits} N={opCount}: {maxMs}ms");
    }
}
