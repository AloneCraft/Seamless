namespace Seamless.Linq.Optimization.PipelineFusion;
public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Where(static value => (value&1)==0).Select(static value => value*2).Consume(this.consumer);
}
