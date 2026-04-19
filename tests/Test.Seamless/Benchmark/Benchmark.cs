using System.Diagnostics;

namespace Test.Seamless.Benchmarks;

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
/// <para><c>Count</c> は各メソッドの計測部分が 10 秒程度で終わる 2 冪の要素数を一括設定する定数。
/// ジェネリックで多数派生するため、同一 Count でも一瞬で終わるテストもあり、
/// 「一番遅いテストが初めて 10 秒を超える Count」を採用している。</para>
/// <para><c>IndexSize==Count</c>。反復回数はテスト側で <see cref="Scaled"/> を使用する。</para>
/// <para>Parallel パラメータは 1,2,4,8,16 のいずれかで、スレッド毎に
/// <c>Store.Create</c> で独立した Store+コレクションを 1 つずつ生成して動作させる。</para>
/// </remarks>
[Collection("Benchmark")]
public abstract class Benchmark(ITestOutputHelper output){
    /// <summary>10 秒程度で最も遅いテストの計測部分が終わる 2 冪の基準件数。</summary>
    private        const int Count = 1<<14;
    /// <summary>IndexSize は Count と同じ。</summary>
    protected        const int IndexSize = Count;
    /// <summary>スケール適用済みの反復回数。</summary>
    protected static long Scaled => BenchmarkScale.ScaleCount(Count);

    /// <summary>(Parallel, MutableFraction) の全組み合わせ。</summary>
    public static TheoryData<int,double> Matrix {
        get {
            var data = new TheoryData<int,double>();
            foreach(var p in new[] { 1,2,4,8, })
                foreach(var mf in new[] { 0.0,0.5,1.0 })
                    data.Add(p,mf);
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
        this.Write($"[{this.GetType().Name}.{label}] P={parallel} MF={mutableFraction} N={opCount}: {maxMs}ms");
    }
}
