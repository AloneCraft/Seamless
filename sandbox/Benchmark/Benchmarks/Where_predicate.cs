namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Where_predicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Where(this.src,x=>(x&1)==0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Where(Enumerable.Where(this.src,x=>(x&1)==0),x=>(x&1)==0)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(this.src,x=>(x&1)==0),x=>(x&1)==0),x=>(x&1)==0)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(Enumerable.Where(this.src,x=>(x&1)==0),x=>(x&1)==0),x=>(x&1)==0),x=>(x&1)==0)
           .Consume(this.consumer);
    }

    void Where1Core(){ this.src.Where(x=>(x&1)==0).Consume(this.consumer); }
    void Where2Core(){ this.src.Where(x=>(x&1)==0).Where(x=>(x&1)==0).Consume(this.consumer); }
    void Where3Core(){ this.src.Where(x=>(x&1)==0).Where(x=>(x&1)==0).Where(x=>(x&1)==0).Consume(this.consumer); }
    void Where4Core(){ this.src.Where(x=>(x&1)==0).Where(x=>(x&1)==0).Where(x=>(x&1)==0).Where(x=>(x&1)==0).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ this.Where1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ this.Where2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ this.Where3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ this.Where4Core(); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Where(x=>(x&1)==0)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Where(x=>(x&1)==0)
           .Consume(this.consumer);
    }
}
