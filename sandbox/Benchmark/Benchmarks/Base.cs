global using ZLinq;

namespace Benchmark.Benchmarks;

public class Base{
    protected readonly Consumer consumer=new Consumer();
    protected int[] src=Enumerable.Range(1,1000000).ToArray();
    protected int[] src2=Enumerable.Range(500001,500000).ToArray();
    protected int[] srcOne=[500000];
    protected int[][] srcNested=Enumerable.Range(1,1000).Select(_=>Enumerable.Range(1,1000).ToArray()).ToArray();
}
