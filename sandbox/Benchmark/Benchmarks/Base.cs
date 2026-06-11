global using ZLinq;

namespace Benchmark.Benchmarks;

public class Base{
    protected readonly Consumer consumer=new();
    //protected int[] src=Enumerable.Range(1,1000000).ToArray();
    //protected int[] src2=Enumerable.Range(500001,500000).ToArray();
    //protected int[] srcOne=[500000];
    //protected int[][] srcNested=Enumerable.Range(1,1000).Select(_=>Enumerable.Range(1,1000).ToArray()).ToArray();
    protected IEnumerable<int> src=Enumerable.Range(1,1000000);
    protected IEnumerable<int> src2=Enumerable.Range(500001,500000);
    protected IEnumerable<int> srcOne=[500000];
    protected IEnumerable<IEnumerable<int>> srcNested=Enumerable.Range(1,1000).Select(_=>Enumerable.Range(1,1000));
}
