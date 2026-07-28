namespace Shared.Linq.Optimization.PredicatePushdown;
public abstract class IEnumerable<T>where T : notnull {
    public class Select_selector(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Select(static value => (Value: value,Number: value))
                                                   .Where(static value => (ToInt(value.Number)&1)==0)
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
    /// <summary>
    /// Union は要素そのものをキーにするため選択の分配則が成り立ち、両辺へ Where を押し下げる。
    /// Concat も分配則自体は成り立つが、述語評価回数も通過要素数も変わらず利得ゼロだったため
    /// 一度実装したうえで対象外にした。押し下げが効かない対照として計測は残す。
    /// </summary>
    public class Union_second(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Union(this.src2Count(4_096))
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
    public class Concat_second(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Concat(this.src2Count(4_096))
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
    /// <summary>
    /// Except/Intersect も要素そのものをキーにするため安全だが、第1ソースのみへ押し下げる
    /// (第2ソース側は集合の意味上そもそも押し下げ不可)。
    /// </summary>
    public class Except_first(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Except(this.src2Count(4_096))
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
    public class Intersect_first(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Intersect(this.src2Count(4_096))
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
}
