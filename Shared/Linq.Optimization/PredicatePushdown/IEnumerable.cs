namespace Shared.Linq.Optimization.PredicatePushdown;
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
    /// <summary>
    /// Join の result selector が作る carrier を逆解析し、述語の依存パターン 4 通りで葉への押し下げを計測する。
    /// bothSides は AND を連言へ分解して outer/inner 双方の葉へ個別に押し下げる経路を通る。
    /// </summary>
    public class Join_inner(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_outerOnly() => this.Where_outerOnly_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_outerOnly_Impl() => this.Source()
                                                   .Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner))
                                                   .Where(static pair => (ToInt(pair.Outer)&1)==0&&ToInt(pair.Outer)>16)
                                                   .Consume(this.consumer);
        [BenchFact,Benchmark]
        public void Where_innerOnly() => this.Where_innerOnly_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_innerOnly_Impl() => this.Source()
                                                   .Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner))
                                                   .Where(static pair => (ToInt(pair.Inner)&1)==0&&ToInt(pair.Inner)>16)
                                                   .Consume(this.consumer);
        [BenchFact,Benchmark]
        public void Where_bothSides() => this.Where_bothSides_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_bothSides_Impl() => this.Source()
                                                   .Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner))
                                                   .Where(static pair => (ToInt(pair.Outer)&1)==0&&(ToInt(pair.Inner)&3)==0)
                                                   .Consume(this.consumer);
        [BenchFact,Benchmark]
        public void Where_constant() => this.Where_constant_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_constant_Impl() => this.Source()
                                                  .Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner))
                                                  .Where(static pair => global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100)
                                                  .Consume(this.consumer);
    }
}
