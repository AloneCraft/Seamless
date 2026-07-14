namespace Seamless.Linq.Optimization;
public abstract class PersistedPipeline<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => this.srcCount(4_096).Where(static value => (ToInt(value)&1)==0).Select(static value => ToInt(value)*2).Consume(this.consumer);
}
