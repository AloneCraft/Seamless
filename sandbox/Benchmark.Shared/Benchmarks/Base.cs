using Tsavorite.SourceGenerator;

namespace Seamless.Benchmarks;

[AttributeUsage(AttributeTargets.Class)]
public sealed class BenchmarkTestIterationsAttribute(int count):Attribute{
    public int Count{get;}=count;
}

public class Base {
    protected readonly Consumer consumer = new();
#if BENCH_AS_TEST
    // テスト（テストエクスプローラでの簡易タイミング計測）では要素数を
    // ベンチクラス毎に変更できる。既定1000万。変えたいクラスは
    // public Xxx():base(要素数){} で指定する。
    private protected readonly int count;
    protected Base(int count=80_000)=>this.count=count;
    private protected int CountForTestTiming(int benchmarkCount)=>this.count;
#else
    // BDN 実ベンチは一律1000万（派生の :Base(N) 指定は無視）。
    // const にすることで生成器のラムダ定数畳み込みが効く。
    private protected const int count=10_000_000;
    protected Base(int count=10_000_000){}
    private protected int CountForTestTiming(int benchmarkCount)=>benchmarkCount;
#endif
    protected IEnumerable<int> srcMinusPlus(int scale) {
        for(var a = 0; a<count*scale; a+=2) {
            yield return -a;
            yield return +a;
        }
    }
    protected IEnumerable<int> src(int scale) => Enumerable.Range(1,count*scale);
    protected IEnumerable<int> src2(int scale) => Enumerable.Range(count*scale/2+1,count*scale/2);
    protected IEnumerable<int> srcOne(int scale) => [0];
    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(count*scale);
        return Enumerable.Range(1,side).Select(_ => Enumerable.Range(1,side));
    }
}
