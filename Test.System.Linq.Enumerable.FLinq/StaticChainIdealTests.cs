using Seamless.Linq;

namespace Test.System.Linq.Enumerable.FLinq;

/// <summary>
/// コンパイル時に傍受したメソッドチェーン(静的)が、手書きの理想ループと同じ結果を返し、
/// 実行時フォールバックを起こさないことを確かめる。
/// ランダムな組み合わせや全組み合わせは、Benchmark.Dynamic.Tests の回帰コーパス(ChainCorpus)が受け持つ。
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
        var idealSum=IdealWhereSelectWhereSum(source);
        Assert.Equal(idealSum,flinqSum);

        var flinqTop5=RunWhereSelectOrderByTake(source);
        var idealTop5=IdealWhereSelectOrderByTake(source);
        Assert.Equal(idealTop5,flinqTop5);

        var flinqGroupSum=RunGroupBySelectWhereSum(source);
        var idealGroupSum=IdealGroupBySelectWhereSum(source);
        Assert.Equal(idealGroupSum,flinqGroupSum);

        Assert.Equal(0,scope.Counter.EnumerableFallback);
    }

    [LinqIntercept(true)]
    private static int RunWhereSelectWhereSum(int[] source)=>
        source.Where(x=>(x&1)==0).Select(x=>x*3+1).Where(x=>x>10).Sum();

    private static int IdealWhereSelectWhereSum(int[] source){
        var acc=0;
        for(var i=0;i<source.Length;i++){
            var x=source[i];
            if((x&1)==0){
                var projected=x*3+1;
                if(projected>10){
                    acc+=projected;
                }
            }
        }
        return acc;
    }

    [LinqIntercept(true)]
    private static int[] RunWhereSelectOrderByTake(int[] source)=>
        source.Where(x=>(x&1)==0).Select(x=>x*3+1).OrderBy(x=>x).Take(5).ToArray();

    private static int[] IdealWhereSelectOrderByTake(int[] source){
        var buffer=new List<int>(source.Length);
        for(var i=0;i<source.Length;i++){
            var x=source[i];
            if((x&1)==0){
                buffer.Add(x*3+1);
            }
        }
        buffer.Sort();
        var count=Math.Min(5,buffer.Count);
        var result=new int[count];
        for(var i=0;i<count;i++){
            result[i]=buffer[i];
        }
        return result;
    }

    [LinqIntercept(true)]
    private static int RunGroupBySelectWhereSum(int[] source)=>
        source.GroupBy(x=>Math.Abs(x)%8).Select(g=>g.Sum()).Where(x=>x>10).Sum();

    private static int IdealGroupBySelectWhereSum(int[] source){
        var groups=new Dictionary<int,int>();
        for(var i=0;i<source.Length;i++){
            var x=source[i];
            var key=Math.Abs(x)%8;
            if(groups.TryGetValue(key,out var current)){
                groups[key]=current+x;
            }else{
                groups[key]=x;
            }
        }
        var total=0;
        foreach(var kv in groups){
            if(kv.Value>10){
                total+=kv.Value;
            }
        }
        return total;
    }
}
