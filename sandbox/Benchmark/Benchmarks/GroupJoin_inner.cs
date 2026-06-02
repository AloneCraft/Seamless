namespace Benchmark;

public class GroupJoin_inner:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupJoin(src,src2,x=>x,x=>x,(outer,inner)=>outer).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.GroupJoin(src2,x=>x,x=>x,(outer,inner)=>outer).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().GroupJoin(src2,x=>x,x=>x,(outer,inner)=>outer).Consume(consumer);
    }
}
