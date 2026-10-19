using Sample.FLinqChainVerifier.Core;
using Seamless.Linq;

namespace Test.System.Linq.Enumerable.FLinq;

public sealed class PairwiseChainIdealTests{
    [Fact]
    public void Stage1_PairwiseCombinations_MatchHandWrittenIdealAndZeroFallback(){
        var runner=new 動的検証ランナー();
        var items=チェーン生成器.段階1ペア網羅を生成(入力ソース種別.Array,0x1018_2026UL);
        Assert.NotEmpty(items);

        foreach(var item in items){
            var result=runner.検証を実行(item);
            Assert.True(result.全メソッド展開済みか,$"Pair {item.クエリ表示式} was not fully lowered. Fallback={result.実行時フォールバック数} Summary={result.結果要約} Code={result.展開コード抜粋}");
            Assert.Equal(0,result.実行時フォールバック数);
            Assert.True(result.全データ結果一致か,$"Pair {item.クエリ表示式} result mismatch: {result.結果要約}");
            Assert.True(result.理想または推定時間マイクロ秒>0.0);
            Assert.Contains("RunFLinqOptCore",result.展開コード抜粋,StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Stage2_RepresentativeABCDChains_MatchHandWrittenIdealAndPairComposition(){
        var runner=new 動的検証ランナー();
        var items=チェーン生成器.段階2代表チェーンを生成(入力ソース種別.Array,0x1018_2026UL);
        Assert.NotEmpty(items);

        foreach(var item in items){
            var result=runner.検証を実行(item);
            Assert.True(result.全メソッド展開済みか,$"Chain {item.クエリ表示式} was not fully lowered. Fallback={result.実行時フォールバック数} Summary={result.結果要約} Code={result.展開コード抜粋}");
            Assert.Equal(0,result.実行時フォールバック数);
            Assert.True(result.全データ結果一致か,$"Chain {item.クエリ表示式} result mismatch: {result.結果要約}");
            Assert.Equal(item.メソッド名一覧.Count-1,item.推定特性.ペア遷移一覧.Count);
            Assert.False(string.IsNullOrWhiteSpace(result.原因診断));
            Assert.False(string.IsNullOrWhiteSpace(result.改善案));
        }
    }

    [Theory]
    [InlineData(入力ソース種別.Array)]
    [InlineData(入力ソース種別.List)]
    [InlineData(入力ソース種別.Range)]
    [InlineData(入力ソース種別.Persisted)]
    [InlineData(入力ソース種別.Tsavorite)]
    public void Stage3_AutomatedCombinations_ExpandWithoutFallbackAndMatchResults(入力ソース種別 sourceKind){
        var runner=new 動的検証ランナー();
        var sampleItems=チェーン生成器.段階3全組み合わせを生成(sourceKind,0x1018_2026UL).Take(6).ToList();
        Assert.Equal(6,sampleItems.Count);

        foreach(var item in sampleItems){
            var result=runner.検証を実行(item);
            Assert.True(result.全メソッド展開済みか,$"[{sourceKind}] {item.クエリ表示式} failed to lower completely. Summary={result.結果要約} Code={result.展開コード抜粋}");
            Assert.Equal(0,result.実行時フォールバック数);
            Assert.True(result.全データ結果一致か,$"[{sourceKind}] {item.クエリ表示式} mismatch: {result.結果要約}");
        }
    }

    [Fact]
    public void Stage4_RandomizedLongChains_DeterministicSeedReplayAndDiagnosticsWork(){
        var runner=new 動的検証ランナー();
        const ulong seed=0x1018_2026UL;

        for(var trial=0;trial<6;trial++){
            var item1=チェーン生成器.段階4ランダムチェーンを生成(入力ソース種別.Array,seed,trial);
            var item2=チェーン生成器.段階4ランダムチェーンを生成(入力ソース種別.Array,seed,trial);

            // 同一シード・同一試行番号から完全に同じチェーンが再現されること
            Assert.Equal(item1.クエリ表示式,item2.クエリ表示式);
            Assert.Equal(item1.クエリ本体コード,item2.クエリ本体コード);
            Assert.True(item1.メソッド名一覧.Count>=4);

            var result=runner.検証を実行(item1);
            Assert.True(result.全メソッド展開済みか,$"Trial #{trial} ({item1.クエリ表示式}) had fallback.");
            Assert.Equal(0,result.実行時フォールバック数);
            Assert.True(result.全データ結果一致か,$"Trial #{trial} ({item1.クエリ表示式}) mismatch: {result.結果要約}");
            Assert.False(string.IsNullOrWhiteSpace(result.原因診断));
            Assert.False(string.IsNullOrWhiteSpace(result.改善案));
            Assert.Contains("Fact",result.再現テストコード,StringComparison.Ordinal);
            Assert.Contains($"0x{item1.シード値:X}UL",result.再現テストコード,StringComparison.OrdinalIgnoreCase);
        }
    }

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
