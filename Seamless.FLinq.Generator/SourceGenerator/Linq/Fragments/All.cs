namespace Seamless.FLinq.Generator;
/*
All_predicate は source.All(predicate) を、最初に false になった要素で短絡する Compute にする。

入力例:
  source.All(x => x.Valid)

生成例:
  foreach(var item in source) {
      if(!predicate(item)) return false;
  }
  return true;

バリエーション:
  - 空 sequence は true。
  - source が TryGetSpan 可能なら span loop を試し、だめなら MoveNext loop に戻る。
  - Range + 単純 predicate は RangePredicateOptimizer でより短い inline にできる。
*/
internal class All_predicate(int 番号,Enumerable source,FuncFragment predicate):BufferedScalar(番号,"bool") {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(predicate is { } __visitorChild_predicate) {
            yield return __visitorChild_predicate;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { predicate },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute(ref {this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute(ref this.{this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? (predicate.ContainsAnchor ? null : RangePredicateOptimizer.RenderAll(range,predicate,番号,destVar,destType)) // .Anchor を含む述語は評価の回数を変えてはいけないので、範囲の形から答えを出す近道を使わない。
                ??(TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"All",rangeLambda)??this.RenderComputeInline(destVar,destType)
                    : this.RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"All",lambda)??this.RenderComputeInline(destVar,destType)
            : this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers {
        get {
            var (preCode,condExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return source.CanTryProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static bool Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                        if(source.TryGetSpan(out var __span)){
                            foreach(var {{this.item}} in __span){
                                {{predicateCheck}} return false;
                            }
                        }else{
                            while(source.TryGetNext(out var __mn1)){
                                var {{this.item}}=__mn1;
                                {{predicateCheck}} return false;
                            }
                        }
                        return true;
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static bool Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                        while(source.TryGetNext(out var __mn2)){
                            var {{this.item}}=__mn2;
                            {{predicateCheck}} return false;
                        }
                        return true;
                    }
                    """;
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
