namespace Seamless.Linq.Optimization;

internal static class OptimizationSettings {
    public const global::Common.LinqOptimizations Value =
#if DISABLE_REWRITE
        global::Common.LinqOptimizations.All&~global::Common.LinqOptimizations.Rewrite;
#elif DISABLE_PIPELINE_FUSION
        global::Common.LinqOptimizations.All&~global::Common.LinqOptimizations.PipelineFusion;
#elif DISABLE_PREDICATE_PUSHDOWN
        global::Common.LinqOptimizations.All&~global::Common.LinqOptimizations.PredicatePushdown;
#else
        global::Common.LinqOptimizations.All;
#endif
}

public abstract class ArrayRewrite<T>(int count = 4_096):global::Common.Base<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
    // Same AlgebraicRewriter branch. [BenchFact,Benchmark]
    public void Where_predicate_Any() => this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Any());
}
public abstract class ArrayPipeline<T>(int count = 4_096):global::Common.Base<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => this.srcCount(4_096).Where(static value => (ToInt(value)&1)==0).Select(static value => ToInt(value)*2).Consume(this.consumer);
}
public abstract class ArrayPushdown<T>(int count = 4_096):global::Common.Base<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.srcCount(4_096).Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}

public abstract class EnumerableSource<T>:global::Common.Base<T> where T : notnull {
    private readonly T[] values;
    protected EnumerableSource(int count = 4_096) : base(count) => this.values=this.srcCount(count);
    protected global::System.Collections.Generic.IEnumerable<T> Source() { for(var index = 0;index<this.values.Length;index++) yield return this.values[index]; }
}
public abstract class EnumerableRewrite<T>(int count = 4_096):EnumerableSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Count1() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.Source().Where(static value => ToInt(value)>2_048).Count());
}
public abstract class EnumerablePipeline<T>(int count = 4_096):EnumerableSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => this.Source().Where(static value => (ToInt(value)&1)==0).Select(static value => ToInt(value)*2).Consume(this.consumer);
}
public abstract class EnumerablePushdown<T>(int count = 4_096):EnumerableSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.Source().Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}

public abstract class ListSource<T>:global::Common.Base<T> where T : notnull {
    private readonly global::System.Collections.Generic.List<T> values;
    protected ListSource(int count = 4_096) : base(count) => this.values= [.. this.srcCount(count)];
    protected global::System.Collections.Generic.List<T> Source() => this.values;
}
public abstract class ListRewrite<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.Source().Where(static value => ToInt(value)>2_048).Count());
}
public abstract class ListPipeline<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => this.Source().Where(static value => (ToInt(value)&1)==0).Select(static value => ToInt(value)*2).Consume(this.consumer);
}
public abstract class ListPushdown<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.Source().Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}

public abstract class RangeRewrite(int count = 4_096):global::Common.Base<int>(count) {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Where(static value => value>2_048).Count());
}
public abstract class RangePipeline(int count = 4_096):global::Common.Base<int>(count) {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Where(static value => (value&1)==0).Select(static value => value*2).Consume(this.consumer);
}
public abstract class RangePushdown(int count = 4_096):global::Common.Base<int>(count) {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Select(static value => (Value: value,Number: value)).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}

public abstract class PersistedRewrite<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
}
public abstract class PersistedPipeline<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => this.srcCount(4_096).Where(static value => (ToInt(value)&1)==0).Select(static value => ToInt(value)*2).Consume(this.consumer);
}
public abstract class PersistedPushdown<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.srcCount(4_096).Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}
public abstract class TsavoriteRewrite<T>(int count = 4_096):global::Common.TsavoriteBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
}
public abstract class TsavoritePipeline<T>(int count = 4_096):global::Common.TsavoriteBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Where_predicate_Select_selector() => this.Where_predicate_Select_selector_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Select_selector_Impl() => this.srcCount(4_096).Where(static value => (ToInt(value)&1)==0).Select(static value => ToInt(value)*2).Consume(this.consumer);
}
public abstract class TsavoritePushdown<T>(int count = 4_096):global::Common.TsavoriteBase<T>(count) where T : notnull {
    [BenchFact, Benchmark] public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Where_predicate_Impl() => this.srcCount(4_096).Select(static value => (Value: value,Number: ToInt(value))).Where(static value => (value.Number&1)==0).Consume(this.consumer);
}
