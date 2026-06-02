namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Where_indexPredicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Where(src,(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Where(Enumerable.Where(src,(x,i)=>i%2==0),(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(src,(x,i)=>i%2==0),(x,i)=>i%2==0),(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(Enumerable.Where(src,(x,i)=>i%2==0),(x,i)=>i%2==0),(x,i)=>i%2==0),(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }
}
