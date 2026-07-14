namespace Seamless.Linq.Optimization.Rewrite;
public abstract class List<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.Source().Where(static value => ToInt(value)>2_048).Count());
}
