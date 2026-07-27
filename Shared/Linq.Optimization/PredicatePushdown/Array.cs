namespace Shared.Linq.Optimization.PredicatePushdown;
/// <summary>
/// Array&lt;int&gt;.Select_selector.Where_predicate というベンチマーク名を出すため、
/// パイプライン演算子の境界（Select_selector / Where_predicate）で型をネストする。
/// </summary>
public abstract class Array<T>where T : notnull {
    public class Select_selector(int count=4_096):global::Common.Base<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.srcCount(4_096)
                                                   .Select(static value => (Value: value,Number: ToInt(value)))
                                                   .Where(static value => (value.Number&1)==0)
                                                   .Consume(this.consumer);
    }
}
