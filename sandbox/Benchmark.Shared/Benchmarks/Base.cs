using Tsavorite.SourceGenerator;

namespace Seamless.Benchmarks;

public class Base {
    protected readonly Consumer consumer = new();
    private protected readonly int count;
    protected Base() : this(10_000_000) { }
    protected Base(int count)=>this.count = count;
#if BENCH_AS_TEST
    // テスト（テストエクスプローラでの簡易タイミング計測）では要素数をベンチクラス毎に変更できる。
    // 既定は1000万。要素数を変えたいベンチクラスは public Xxx():base(要素数){} で指定する。
    // ソースは instance メソッドなので、生成器は __Method_Base_src(@this,scale) として
    // 派生インスタンスの count を読む。反復・ウォームアップ回数は BenchmarkParameters
    //（環境変数 SEAMLESS_BENCHMARK_*）で上書きできる。
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
#else
    // 本番BDNでは要素数は const 100万固定。コンストラクタ引数（:base(N)）は無視される。
    protected IEnumerable<int> srcMinusPlus(int scale) {
        for(var a = 0; a<count*scale; a+=2) {
            yield return -a;
            yield return +a;
        }
    }
    protected IEnumerable<int> src(int scale) => Enumerable.Range(1,count*scale);
    protected IEnumerable<int> src2(int scale) => Enumerable.Range(count*scale/2+1,count*scale/2);
    protected static IEnumerable<int> srcOne(int scale) => [0];
    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(count*scale);
        return Enumerable.Range(1,side).Select(_ => Enumerable.Range(1,side));
    }
#endif
}
