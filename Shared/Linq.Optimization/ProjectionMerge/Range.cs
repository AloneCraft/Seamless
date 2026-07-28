namespace Shared.Linq.Optimization.ProjectionMerge;
public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
    // Select(carrier).Select(member) の融合。carrier 構築が丸ごと消えることを計測する。
    [BenchFact, Benchmark] public void Select_selector_Select_member() => this.Select_selector_Select_member_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Select_member_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Select(static value => (Value: value,Number: value)).Select(static pair => pair.Number).Consume(this.consumer);
    // let 句相当。Tier2(メソッド呼び出し)が 1 個で後続がちょうど 1 回読むので融合できる。
    [BenchFact, Benchmark] public void Select_selector_Select_computed() => this.Select_selector_Select_computed_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Select_selector_Select_computed_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Select(static value => (Source: value,Doubled: global::Common.HoistInvariant.Value()*value)).Select(static pair => pair.Doubled+pair.Source).Consume(this.consumer);
    // Join の result selector が作る carrier を後続 Select が吸収する経路。クエリ式 join の lowering 形と同じ。
    [BenchFact, Benchmark] public void Join_inner_Select_outerMember() => this.Join_inner_Select_outerMember_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Select_outerMember_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Join(this.SecondRange(),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Select(static pair => pair.Outer).Consume(this.consumer);
    [BenchFact, Benchmark] public void Join_inner_Select_bothMembers() => this.Join_inner_Select_bothMembers_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Join_inner_Select_bothMembers_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).Join(this.SecondRange(),static outer => outer,static inner => inner,static (outer,inner) => (Outer: outer,Inner: inner)).Select(static pair => pair.Outer+pair.Inner).Consume(this.consumer);
    // 位置引数 record struct の carrier はベンチマークでは扱わない。
    // 生成器が「new 型名(..) の型名ノードを不変式としてホイストしてしまう」既存バグを踏み、
    // 射影融合を無効にした対照側(NoProjectionMerge)で carrier が残ると __hoist4(outer,inner) という
    // 不正な C# になる。射影融合とは無関係の別バグ。
    // record struct / record carrier の意味保存と融合形状は
    // Test/System/Linq/Intercept/Optimization/JoinSelectFusion.CarrierShapes と
    // Test/SourceGenerator/ProjectionMergeShapeTests で固定してある。
    // SelectMany の result selector も同型。collection selector は 1 要素に絞って carrier 構築の有無に寄せる。
    [BenchFact, Benchmark] public void SelectMany_collection_Select_member() => this.SelectMany_collection_Select_member_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void SelectMany_collection_Select_member_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(4_096)).SelectMany(static outer => global::System.Linq.Enumerable.Repeat(outer,1),static (outer,inner) => (Outer: outer,Inner: inner)).Select(static pair => pair.Inner).Consume(this.consumer);
    // src2Count と同じ「後半だけ重なる第 2 系列」を Range で作る。
    [LinqIntercept(false)] private global::System.Collections.Generic.IEnumerable<int> SecondRange() {
        var length = this.EffectiveSourceCount(4_096);
        return global::System.Linq.Enumerable.Range(length/2+1,length/2);
    }
}
