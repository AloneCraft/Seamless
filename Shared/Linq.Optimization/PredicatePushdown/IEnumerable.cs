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
    /// <summary>
    /// Distinct は要素そのものをキーにするため代表選択が述語に影響されず、無条件で押し下げる。
    /// 押し下げると Distinct のソート済み int スパン直出し高速パスを失うので、その損得をここで計測する。
    /// </summary>
    public class Distinct_source(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Distinct()
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
    /// <summary>
    /// Reverse は要素数を保つので述語評価回数が変わらず、押し下げは原理的に損しない。
    /// ただし span 逆走査の高速パスは失うため、Order と同型のガードが要るかをここで判断する。
    /// </summary>
    public class Reverse_source(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Reverse()
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
    /// <summary>
    /// Order 系の前への押し下げ。2026-10-04 までは、ソースがコンパイル時にスパンを出せるときは昇順の span 高速経路を守るため
    /// 押し下げず、この行はその対照だった。今は押し下げ、並べ替えの段が溜めた値が既に並んでいるかを確かめる。
    /// </summary>
    public class Order_source(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Order()
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
    /// <summary>
    /// Order のソースが LINQ 呼び出しになるとガードが外れて押し下がる形。
    /// <br/>上流に恒等 Select を置くのは不可。AlgebraicRewriter が押し下げより前のパスで
    /// 恒等 Select と AsEnumerable を除去してしまい、ガードが再び効いてしまう。
    /// <br/>Skip(0) は除去対象でも押し下げ対象でもないので、Order 押し下げだけを単独で計測できる。
    /// </summary>
    public class Skip_count_Order_source(int count=4_096):EnumerableSource<T>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate() => this.Where_predicate_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Where_predicate_Impl() => this.Source()
                                                   .Skip(0)
                                                   .Order()
                                                   .Where(static value => (ToInt(value)&1)==0)
                                                   .Consume(this.consumer);
    }
}
