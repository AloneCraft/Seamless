namespace Seamless.Linq.Optimization.PredicatePushdown;
public abstract class IEnumerable<T>(int count = 4_096):EnumerableSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.Source().Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}
