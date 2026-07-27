namespace Shared.Linq.Optimization.PipelineFusion;
public abstract class Range{
    public class Where_predicate(int count=4_096):global::Common.Base<int>(count) {
        [BenchFact,Benchmark]
        public void Select_selector() => this.Select_selector_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_selector_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096))
                                                   .Where(static value => (value&1)==0)
                                                   .Select(static value => value*2)
                                                   .Consume(this.consumer);
    }
}
