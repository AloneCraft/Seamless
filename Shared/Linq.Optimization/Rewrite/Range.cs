namespace Shared.Linq.Optimization.Rewrite;
public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Where(static value => value>2_048).Count());
}
