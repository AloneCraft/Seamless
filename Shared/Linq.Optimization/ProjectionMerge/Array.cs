namespace Shared.Linq.Optimization.ProjectionMerge;
/// <summary>
/// Array&lt;int&gt;.Join_inner.Select_outerMember というベンチマーク名を出すため、
/// パイプライン演算子の境界（Select_selector / Join_inner / SelectMany_collection）で型をネストする。
/// </summary>
public abstract class Array<T>where T : notnull {
    /// <summary>
    /// Select(carrier).Select(member) の融合。carrier 構築が丸ごと消えることを計測する。
    /// </summary>
    public class Select_selector(int count=4_096):global::Common.Base<T>(count) {
        [BenchFact,Benchmark]
        public void Select_member() => this.Select_member_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_member_Impl() => this.srcCount(4_096)
                                                 .Select(static value => (Value: value,Number: ToInt(value)))
                                                 .Select(static pair => pair.Number)
                                                 .Consume(this.consumer);
        // let 句相当。Tier2(メソッド呼び出し)が 1 個で後続がちょうど 1 回読むので融合できる。
        [BenchFact,Benchmark]
        public void Select_computed() => this.Select_computed_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_computed_Impl() => this.srcCount(4_096)
                                                   .Select(static value => (Source: value,Doubled: ToInt(value)*2))
                                                   .Select(static pair => pair.Doubled+ToInt(pair.Source))
                                                   .Consume(this.consumer);
    }
    /// <summary>
    /// Join の result selector が作る carrier を後続 Select が吸収する経路を計測する。
    /// クエリ式の join 句は必ず透過 carrier + 末尾 Select に lowering されるので、これが最大の利得経路。
    /// </summary>
    public class Join_inner(int count=4_096):global::Common.Base<T>(count) {
        [BenchFact,Benchmark]
        public void Select_outerMember() => this.Select_outerMember_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_outerMember_Impl() => this.srcCount(4_096)
                                                      .Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner))
                                                      .Select(static pair => ToInt(pair.Outer))
                                                      .Consume(this.consumer);
        [BenchFact,Benchmark]
        public void Select_bothMembers() => this.Select_bothMembers_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_bothMembers_Impl() => this.srcCount(4_096)
                                                      .Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner))
                                                      .Select(static pair => ToInt(pair.Outer)+ToInt(pair.Inner))
                                                      .Consume(this.consumer);
        // 位置引数 record struct の carrier は Range(型引数が閉じている)側で計測する。
        // ジェネリックなベンチマーク型からだと new CarrierPair<T,T>(..) の T が生成コードへ正しく描画されない
        // 既存の生成器バグ(開いた型引数を持つ new 式)を踏むため、ここでは扱わない。射影融合とは無関係。
    }
    /// <summary>
    /// SelectMany の result selector も同型で吸収できることを計測する。
    /// collection selector は 1 要素に絞り、計測対象を carrier 構築の有無に寄せる。
    /// </summary>
    public class SelectMany_collection(int count=4_096):global::Common.Base<T>(count) {
        [BenchFact,Benchmark]
        public void Select_member() => this.Select_member_Impl();
        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
        private void Select_member_Impl() => this.srcCount(4_096)
                                                 .SelectMany(static outer => global::System.Linq.Enumerable.Repeat(outer,1),static (outer,inner) => (Outer: outer,Inner: inner))
                                                 .Select(static pair => ToInt(pair.Inner))
                                                 .Consume(this.consumer);
    }
}
