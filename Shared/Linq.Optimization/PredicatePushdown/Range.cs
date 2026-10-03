namespace Shared.Linq.Optimization.PredicatePushdown;
public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Select(static value => (Value: value,Number: value)).Where(static value => (value.Number&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_outerOnly() => this.Join_inner_Where_outerOnly_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_outerOnly_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Join(this.SecondRange(),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (pair.Outer&1)==0&&pair.Outer>16).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_innerOnly() => this.Join_inner_Where_innerOnly_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_innerOnly_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Join(this.SecondRange(),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (pair.Inner&1)==0&&pair.Inner>16).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_bothSides() => this.Join_inner_Where_bothSides_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_bothSides_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Join(this.SecondRange(),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (pair.Outer&1)==0&&(pair.Inner&3)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_constant() => this.Join_inner_Where_constant_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_constant_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Join(this.SecondRange(),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100).Consume(this.consumer);
    // Union は両辺へ、Except/Intersect は第1ソースへ Where を押し下げる。
    // Concat は一度両辺押し下げを実装したが、述語評価回数も通過要素数も変わらず利得ゼロだったため
    // 対象外にした。押し下げが効かない対照として計測は残す。
    [BenchFact, Benchmark] public void Union_second_Where_predicate() => this.Union_second_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Union_second_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Union(this.SecondRange()).Where(static value => (value&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Concat_second_Where_predicate() => this.Concat_second_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Concat_second_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Concat(this.SecondRange()).Where(static value => (value&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Except_first_Where_predicate() => this.Except_first_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Except_first_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Except(this.SecondRange()).Where(static value => (value&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Intersect_first_Where_predicate() => this.Intersect_first_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Intersect_first_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Intersect(this.SecondRange()).Where(static value => (value&1)==0).Consume(this.consumer);
    // Distinct/Reverse は無条件で押し下げる(要素そのものがキー／要素数を保つ)。どちらも span 系の
    // 高速パスを失うので、Order と同型のガードを足すべきかをこの計測で判断する。
    // Order 系の前へも押し下げる(2026-10-04 までは、ソースがコンパイル時にスパンを出せるときは昇順の span 高速経路を
    // 守るため押し下げず、Order_Where_predicate はその対照だった)。今は押し下げ、並べ替えの段が溜めた値が既に並んでいるかを
    // 確かめる。Skip_count_Order_Where_predicate は Order のソースが LINQ 呼び出しになる形。
    // Skip(0) は除去対象でも押し下げ対象でもないので、Order 押し下げだけを単独で計測できる。
    [BenchFact, Benchmark] public void Distinct_Where_predicate() => this.Distinct_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Distinct_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Distinct().Where(static value => (value&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Reverse_Where_predicate() => this.Reverse_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Reverse_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Reverse().Where(static value => (value&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Order_Where_predicate() => this.Order_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Order_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Order().Where(static value => (value&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Skip_count_Order_Where_predicate() => this.Skip_count_Order_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Skip_count_Order_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Skip(0).Order().Where(static value => (value&1)==0).Consume(this.consumer);
    // src2Count と同じ「後半だけ重なる第 2 系列」を Range で作る。
    [LinqIntercept(false)] private global::System.Collections.Generic.IEnumerable<int> SecondRange() {
        var length = this.EffectiveSourceCount(4_096);
        return global::System.Linq.Enumerable.Range(length/2+1,length/2);
    }
}
