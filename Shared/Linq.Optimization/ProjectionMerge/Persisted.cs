namespace Shared.Linq.Optimization.ProjectionMerge;
public abstract class Persisted<T>(int count = 4_096):global::Common.PersistedBase<T>(count) where T : notnull {
    // Select(carrier).Select(member) の融合。carrier 構築が丸ごと消えることを計測する。
    [BenchFact, Benchmark] public void Select_selector_Select_member() => this.Select_selector_Select_member_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Select_member_Impl() => this.srcCount(4_096).Select(static value => (Value: value,Number: ToInt(value))).Select(static pair => pair.Number).Consume(this.consumer);
    // let 句相当。Tier2(メソッド呼び出し)が 1 個で後続がちょうど 1 回読むので融合できる。
    [BenchFact, Benchmark] public void Select_selector_Select_computed() => this.Select_selector_Select_computed_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Select_computed_Impl() => this.srcCount(4_096).Select(static value => (Source: value,Doubled: ToInt(value)*2)).Select(static pair => pair.Doubled+ToInt(pair.Source)).Consume(this.consumer);
    // Join の result selector が作る carrier を後続 Select が吸収する経路。クエリ式 join の lowering 形と同じ。
    [BenchFact, Benchmark] public void Join_inner_Select_outerMember() => this.Join_inner_Select_outerMember_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Select_outerMember_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Select(static pair => ToInt(pair.Outer)).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Select_bothMembers() => this.Join_inner_Select_bothMembers_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Select_bothMembers_Impl() => this.srcCount(4_096).Join(this.src2Count(4_096),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Select(static pair => ToInt(pair.Outer)+ToInt(pair.Inner)).Consume(this.consumer);
    // 位置引数 record struct の carrier は Range(型引数が閉じている)側で計測する。
    // ジェネリックなベンチマーク型からだと new CarrierPair<T,T>(..) の T が生成コードへ正しく描画されない
    // 既存の生成器バグ(開いた型引数を持つ new 式)を踏むため、ここでは扱わない。射影融合とは無関係。
    // SelectMany の result selector も同型。collection selector は 1 要素に絞って carrier 構築の有無に寄せる。
    [BenchFact, Benchmark] public void SelectMany_collection_Select_member() => this.SelectMany_collection_Select_member_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void SelectMany_collection_Select_member_Impl() => this.srcCount(4_096).SelectMany(static outer => global::System.Linq.Enumerable.Repeat(outer,1),static (outer,inner) => (Outer: outer,Inner: inner)).Select(static pair => ToInt(pair.Inner)).Consume(this.consumer);
}
