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
    // src2Count と同じ「後半だけ重なる第 2 系列」を Range で作る。
    [LinqIntercept(false)] private global::System.Collections.Generic.IEnumerable<int> SecondRange() {
        var length = this.EffectiveSourceCount(4_096);
        return global::System.Linq.Enumerable.Range(length/2+1,length/2);
    }
}
