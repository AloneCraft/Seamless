namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Where_predicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Where(src,x=>(x&1)==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Where(Enumerable.Where(src,x=>(x&1)==0),x=>(x&1)==0)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(src,x=>(x&1)==0),x=>(x&1)==0),x=>(x&1)==0)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(Enumerable.Where(src,x=>(x&1)==0),x=>(x&1)==0),x=>(x&1)==0),x=>(x&1)==0)
           .Consume(consumer);
    }

    void Where1Core(){ this.src.Where(x=>(x&1)==0).Consume(this.consumer); }
    void Where2Core(){ this.src.Where(x=>(x&1)==0).Where(x=>(x&1)==0).Consume(this.consumer); }
    void Where3Core(){ this.src.Where(x=>(x&1)==0).Where(x=>(x&1)==0).Where(x=>(x&1)==0).Consume(this.consumer); }
    void Where4Core(){ this.src.Where(x=>(x&1)==0).Where(x=>(x&1)==0).Where(x=>(x&1)==0).Where(x=>(x&1)==0).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ Where1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ Where2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ Where3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ Where4Core(); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable()
           .Where(x=>(x&1)==0)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable()
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable()
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable()
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Consume(consumer);
    }
}
