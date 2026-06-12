using Tsavorite.SourceGenerator;

namespace Seamless.Benchmarks;

public class Base {
    protected readonly Consumer consumer = new();
    // BENCH_AS_TEST（テストエクスプローラでの簡易タイミング計測）では BDN と同じ100万に揃える。
    // 反復・ウォームアップ回数は BenchmarkParameters（環境変数 SEAMLESS_BENCHMARK_*）で上書きできる。
    private protected const int count =
#if BENCH_AS_TEST
        1_000_000
#else
        1_000_000
#endif
        ;
    //protected int[] src=Enumerable.Range(1,1000000).ToArray();
    //protected int[] src2=Enumerable.Range(500001,500000).ToArray();
    //protected int[] srcOne=[500000];
    //protected int[][] srcNested=Enumerable.Range(1,1000).Select(_=>Enumerable.Range(1,1000).ToArray()).ToArray();
    // 要素はフィールドで保持する（ソースジェネレータが値として捕捉できる形）。
    // 要素数を変えたいベンチクラスは public Xxx():base(要素数){} のように指定する。
    // 未指定の既定は本番BDN・BENCH_AS_TEST いずれも100万。
    protected static IEnumerable<int> srcMinusPlus(int scale) {
        for(var a = 0; a<count*scale; a+=2) {
            yield return -a;
            yield return +a;
        }
    }
    protected static IEnumerable<int> src(int scale) => Enumerable.Range(1,count*scale);
    protected static IEnumerable<int> src2(int scale) => Enumerable.Range(count*scale/2+1,count*scale/2);
    protected static IEnumerable<int> srcOne(int scale) => [0];
    [NoLinqRewrite]
    protected static IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(count*scale);
        return Enumerable.Range(1,side).Select(_ => Enumerable.Range(1,side));
    }

    //protected readonly IEnumerable<int> src;
    //protected readonly IEnumerable<int> src2;
    //protected readonly IEnumerable<int> srcOne;
    //protected readonly IEnumerable<IEnumerable<int>> srcNested;
    protected Base() : this(1_000_000) { }
    protected Base(int count) {
        //var side=(int)Math.Sqrt(count);
        //src(1)=Enumerable.Range(1,count);
        //src2(1)=Enumerable.Range(count/2+1,count/2);
        //srcOne(1)=[count/2];
        //srcNested(1)=Enumerable.Range(1,side).Select(_=>Enumerable.Range(1,side));
    }
}
