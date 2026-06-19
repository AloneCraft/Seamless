namespace Labo.Shared.Linq.Enumerable;

public abstract class SystemEnumerableMethods(int count):global::Benchmark.Shared.Base(count) {
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory("Select")]
    public void Select_selector() => this.Select_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Select_selector_Impl() => this.src(1).AsX<int>().Select(static x => x+x).Consume(this.consumer);
}
