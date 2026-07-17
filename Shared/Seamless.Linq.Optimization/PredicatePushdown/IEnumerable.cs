namespace Seamless.Linq.Optimization.PredicatePushdown;
public abstract class IEnumerable<T>where T : notnull {
    public class Select_selector(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Select(static value => (Value: value,Number: ToInt(value)))
                                                   .Where(static value => (value.Number&1)==0)
                                                   .Consume(this.consumer);
    }
}
