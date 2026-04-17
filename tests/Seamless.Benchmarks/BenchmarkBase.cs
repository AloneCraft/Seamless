using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Seamless.Collections.Benchmarks;

/// <summary>
/// 全ベンチマーク共通の基底。パラメータ組 <c>(Parallel, MutableFraction)</c> を持ち、
/// スレッド毎に独立した Store + Collection を生成するためのヘルパを提供する。
/// </summary>
/// <remarks>
/// パラメータグリッドは MutableFraction ∈ {0, 0.5, 1.0}、Parallel ∈ {1,2,4,8,16}。
/// Managed/Unmanaged と Dictionary/HashSet の切替は派生型の型引数で行う。
/// </remarks>
[Collection("Benchmark")]
public abstract class BenchmarkBase {
    private readonly ITestOutputHelper output;
    protected BenchmarkBase(ITestOutputHelper output) {
        this.output=output;
    }

    /// <summary>テスト出力。</summary>
    protected void Write(string message) => this.output.WriteLine(message);

    /// <summary>Parallel と MutableFraction の全組合せを返す (15 件)。</summary>
    public static IEnumerable<object[]> Matrix() {
        foreach(var mutableFraction in new[] { 0.0,0.5,1.0 }) {
            foreach(var parallel in new[] { 1,2,4,8,16 }) {
                yield return [parallel,mutableFraction];
            }
        }
    }

    /// <summary>
    /// MutableFraction 付きで Factory を作成する。mutableFraction&lt;1 の場合は
    /// ログ/オブジェクトログ用の一時ディレクトリを割り当て、戻り値に付随させる。
    /// </summary>
    protected static Factory CreateFactory(double mutableFraction, int elementCount) {
        var indexSize = (long)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1,elementCount));
        // MutableFraction=1.0 なら全件メモリに収まるサイズ、それ以外は半分が mutable に入る程度に。
        // Seamless.Const.LogMemorySize は最小値として。
        var memorySize = Math.Max(Seamless.Const.LogMemorySize<<11,elementCount*64L);
        return Factory.Create(
            IndexSize: indexSize,
            MemorySize: memorySize,
            MutableFraction: mutableFraction);
    }

    /// <summary>一時ディレクトリ付きのログ/オブジェクトログデバイスを作成する。</summary>
    protected static (string dir, IDevice log, IDevice obj) CreateDevices() {
        var dir = Path.Combine(Path.GetTempPath(),"SeamlessBench_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(dir,"log.data"),deleteOnClose: true);
        var obj = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(dir,"obj.data"),deleteOnClose: true);
        return (dir,log,obj);
    }

    /// <summary>ディレクトリ削除を例外ハンドリング付きで。</summary>
    protected static void TryDeleteDir(string? dir) {
        if(dir is null) return;
        try { if(Directory.Exists(dir)) Directory.Delete(dir,recursive: true); } catch { }
    }

    /// <summary>
    /// スレッド毎に <paramref name="setupPerThread"/> で (Store, Collection) を組み立て、
    /// <paramref name="measure"/> を計測区間として Parallel.For で同時実行する。
    /// 計測は <see cref="Parallel.For(int,int,ParallelOptions,Action{int})"/> による壁時計時間。
    /// </summary>
    /// <remarks>計測開始前に全スレッド分のセットアップを完了させ、計測終了後に Disposable を破棄する。</remarks>
    protected void RunParallel<TState>(
        int parallel,
        Func<int,TState> setupPerThread,
        Action<int,TState> measure,
        Action<TState>? teardown,
        string label,
        long measuredOpsTotal
    ) where TState:class {
        var states = new TState[parallel];
        for(var i = 0; i<parallel; i++) states[i]=setupPerThread(i);
        var sw = Stopwatch.StartNew();
        if(parallel==1) {
            measure(0,states[0]);
        } else {
            Parallel.For(0,parallel,
                new ParallelOptions { MaxDegreeOfParallelism=parallel },
                t => measure(t,states[t]));
        }
        sw.Stop();
        var elapsedMs = sw.ElapsedMilliseconds;
        var opsPerSec = measuredOpsTotal*1000.0/Math.Max(1,elapsedMs);
        this.Write($"[{label}] p={parallel} ops={measuredOpsTotal:N0} elapsed={elapsedMs}ms ({opsPerSec:N0}ops/s)");
        if(teardown!=null) {
            foreach(var s in states) teardown(s);
        }
    }
}
