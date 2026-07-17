namespace Seamless.Linq.Optimization.PipelineFusion;
public abstract class Persisted<T>where T : notnull {
    public class Where_predicate(int count=4_096):global::Common.PersistedBase<T>(count) {
        [BenchFact,Benchmark]
        public void Select_selector() => this.Select_selector_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_selector_Impl() => this.srcCount(4_096)
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Select(static value => ToInt(value)*2)
                                                   .Consume(this.consumer);
    }
}
