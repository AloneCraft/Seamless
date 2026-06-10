namespace Benchmark.Benchmarks;

public class Join_outerKey_Hoisting:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Join(this.src,this.src2,o=>(o+HoistInvariant.Value())%1000,i=>i%1000,(o,i)=>o+i).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.Join(this.src2,o=>(o+HoistInvariant.Value())%1000,i=>i%1000,(o,i)=>o+i).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).Join(this.src2,o=>(o+HoistInvariant.Value())%1000,i=>i%1000,(o,i)=>o+i).Consume(this.consumer);
    }
}
