using Seamless.Linq;
using LinqInterceptAttribute=Seamless.FLinq.LinqInterceptAttribute;
using ClassInt32=Seamless.TestKit.ClassInt32;

namespace Test.FLinqBackends.SystemArray;

/// <summary>
/// 静的コンパイルのインターセプターが期待値を返し、実行時フォールバックを起こさないことを確かめる。
/// 同じ3ケースの手書き参照と境界値は Benchmark.Dynamic の固定ケースで比較する。
/// </summary>
public sealed class StaticChainIdealTests{
    [Fact]
    public void StaticIntercept_ABCD_Chains_MatchHandWrittenIdealLoops(){
        var source=new int[256];
        for(var i=0;i<source.Length;i++){
            source[i]=(i*37+13)%97-20;
        }

        using var scope=LinqCounterScope.Start();
        var flinqSum=RunWhereSelectWhereSum(source);
        Assert.Equal(11683,flinqSum);

        var flinqTop5=RunWhereSelectOrderByTake(source);
        Assert.Equal([-59,-59,-59,-53,-53],flinqTop5);

        var flinqGroupSum=RunGroupBySelectWhereSum(source);
        Assert.Equal(7065,flinqGroupSum);

        Assert.Equal(0,scope.Counter.EnumerableFallback);
    }

    [LinqIntercept(true)]
    private static int RunWhereSelectWhereSum(int[] source)=>
        source.Where(x=>(x&1)==0).Select(x=>x*3+1).Where(x=>x>10).Sum();

    [LinqIntercept(true)]
    private static int[] RunWhereSelectOrderByTake(int[] source)=>
        source.Where(x=>(x&1)==0).Select(x=>x*3+1).OrderBy(x=>x).Take(5).ToArray();

    [LinqIntercept(true)]
    private static int RunGroupBySelectWhereSum(int[] source)=>
        source.GroupBy(x=>Math.Abs(x)%8).Select(g=>g.Sum()).Where(x=>x>10).Sum();

}
