namespace Shared.Linq.Optimization.PredicatePushdown;
public abstract class Persisted<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.srcCount(4_096).Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_outerOnly() => this.Join_inner_Where_outerOnly_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_outerOnly_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (ToInt(pair.Outer)&1)==0&&ToInt(pair.Outer)>16).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_innerOnly() => this.Join_inner_Where_innerOnly_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_innerOnly_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (ToInt(pair.Inner)&1)==0&&ToInt(pair.Inner)>16).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_bothSides() => this.Join_inner_Where_bothSides_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_bothSides_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => (ToInt(pair.Outer)&1)==0&&(ToInt(pair.Inner)&3)==0).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Where_constant() => this.Join_inner_Where_constant_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Where_constant_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Where(static pair => global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100).Consume(this.consumer);
}
