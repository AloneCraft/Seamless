namespace Shared.Linq.Optimization.PredicatePushdown;
public abstract class Persisted<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.srcCount(4_096).Select(static value => (Value: value,Number: value)).Where(static value => (ToInt(value.Number)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_outerOnly() => this.Join_inner_Where_outerOnly_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_outerOnly_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (ToInt(pair.Outer)&1)==0&&ToInt(pair.Outer)>16).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_innerOnly() => this.Join_inner_Where_innerOnly_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_innerOnly_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (ToInt(pair.Inner)&1)==0&&ToInt(pair.Inner)>16).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_bothSides() => this.Join_inner_Where_bothSides_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_bothSides_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (ToInt(pair.Outer)&1)==0&&(ToInt(pair.Inner)&3)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_constant() => this.Join_inner_Where_constant_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_constant_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100).Consume(this.consumer);
    // Union は両辺へ、Except/Intersect は第1ソースへ Where を押し下げる。
    // Concat は一度両辺押し下げを実装したが、述語評価回数も通過要素数も変わらず利得ゼロだったため
    // 対象外にした。押し下げが効かない対照として計測は残す。
    [BenchFact, Benchmark] public void Union_second_Where_predicate() => this.Union_second_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Union_second_Where_predicate_Impl() => this.srcCount(4_096).Union(this.src2Count(4_096)).Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Concat_second_Where_predicate() => this.Concat_second_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Concat_second_Where_predicate_Impl() => this.srcCount(4_096).Concat(this.src2Count(4_096)).Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Except_first_Where_predicate() => this.Except_first_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Except_first_Where_predicate_Impl() => this.srcCount(4_096).Except(this.src2Count(4_096)).Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Intersect_first_Where_predicate() => this.Intersect_first_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Intersect_first_Where_predicate_Impl() => this.srcCount(4_096).Intersect(this.src2Count(4_096)).Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    // Distinct/Reverse は無条件で押し下げる(要素そのものがキー／要素数を保つ)。どちらも span 系の
    // 高速パスを失うので、Order と同型のガードを足すべきかをこの計測で判断する。
    // Order 系だけは KeepsSortedSpanFastPath が止める。Order_Where_predicate はガードが効く対照で
    // 差が出ないことが期待値、Skip_count_Order_Where_predicate は Order のソースが LINQ 呼び出しに
    // なるためガードが外れて押し下がる形。上流に恒等 Select を置くのは不可。AlgebraicRewriter が
    // 押し下げより前のパスで恒等 Select と AsEnumerable を除去してしまい、ガードが再び効いてしまう。
    // Skip(0) は除去対象でも押し下げ対象でもないので、Order 押し下げだけを単独で計測できる。
    [BenchFact, Benchmark] public void Distinct_Where_predicate() => this.Distinct_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Distinct_Where_predicate_Impl() => this.srcCount(4_096).Distinct().Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Reverse_Where_predicate() => this.Reverse_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Reverse_Where_predicate_Impl() => this.srcCount(4_096).Reverse().Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Order_Where_predicate() => this.Order_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Order_Where_predicate_Impl() => this.srcCount(4_096).Order().Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Skip_count_Order_Where_predicate() => this.Skip_count_Order_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Skip_count_Order_Where_predicate_Impl() => this.srcCount(4_096).Skip(0).Order().Where(static value => (ToInt(value)&1)==0).Consume(this.consumer);
}
