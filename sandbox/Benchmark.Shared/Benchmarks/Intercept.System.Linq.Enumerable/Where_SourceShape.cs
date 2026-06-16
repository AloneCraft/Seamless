namespace Benchmark.Intercept.System.Linq.Enumerable;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Where_SourceShape:Base {
    private int[] array = [];
    private List<int> list = [];
    private int sourceCount;

    public Where_SourceShape():base(200_000) {
        this.Initialize();
    }

    private void Initialize() {
        this.sourceCount=this.CountForTestTiming(1_000_000);
        this.array=global::System.Linq.Enumerable.Range(1,this.sourceCount).ToArray();
        this.list=this.array.ToList();
    }

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory("Array")]
    public void LinqArray()=>this.array.Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory("Array")]
    public void FLinqArray()=>this.LinqArray();

    [BenchFact,Benchmark,BenchmarkCategory("Array")]
    public void ZLinqArray()=>this.array.AsValueEnumerable().Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory("List")]
    public void LinqList()=>this.list.Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory("List")]
    public void FLinqList()=>this.LinqList();

    [BenchFact,Benchmark,BenchmarkCategory("List")]
    public void ZLinqList()=>this.list.AsValueEnumerable().Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory("Range")]
    public void LinqRange()=>global::System.Linq.Enumerable.Range(1,this.sourceCount).Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory("Range")]
    public void FLinqRange()=>this.LinqRange();

    [BenchFact,Benchmark,BenchmarkCategory("Range")]
    public void ZLinqRange()=>global::System.Linq.Enumerable.Range(1,this.sourceCount).AsValueEnumerable().Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory("Yield")]
    public void LinqYield()=>this.YieldRange().Where(static x=>(x&1)==0).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory("Yield")]
    public void FLinqYield()=>this.LinqYield();

    [BenchFact,Benchmark,BenchmarkCategory("Yield")]
    public void ZLinqYield()=>this.YieldRange().AsValueEnumerable().Where(static x=>(x&1)==0).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match() {
        this.AssertResults(this.LinqArray,this.FLinqArray,this.FLinqArrayNoOptimized,this.ZLinqArray);
        this.AssertResults(this.LinqList,this.FLinqList,this.FLinqListNoOptimized,this.ZLinqList);
        this.AssertResults(this.LinqRange,this.FLinqRange,this.FLinqRangeNoOptimized,this.ZLinqRange);
        this.AssertResults(this.LinqYield,this.FLinqYield,this.FLinqYieldNoOptimized,this.ZLinqYield);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqArrayNoOptimized()=>this.LinqArray();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqListNoOptimized()=>this.LinqList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqRangeNoOptimized()=>this.LinqRange();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqYieldNoOptimized()=>this.LinqYield();

    private IEnumerable<int> YieldRange() {
        for(var i = 1;i<=this.sourceCount;i++) {
            yield return i;
        }
    }
}
