namespace Seamless.FLinq.Linq.Fragments;
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
public class All_predicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({e}{ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderAll(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? RenderDirectEnumerableCallInline(source,destVar,destType,"All",rangeLambda) ?? RenderComputeInline(destVar,destType)
                    : RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"All",lambda) ?? RenderComputeInline(destVar,destType)
            : RenderComputeInline(destVar,destType);
    protected override string ComputeMembers {
        get {
            var (preCode, condExpr) = FuncInvokeConditionStatic(predicate, nameof(predicate), temp, item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return source.CanTryProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                        if(source.TryGetSpan(out var __span)){
                            foreach(var {{item}} in __span){
                                {{predicateCheck}} return false;
                            }
                        }else{
                            while(source.MoveNext()) {
                                var {{item}}=source.Current;
                                {{predicateCheck}} return false;
                            }
                        }
                        return true;
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                        while(source.MoveNext()) {
                            var {{item}}=source.Current;
                            {{predicateCheck}} return false;
                        }
                        return true;
                    }
                    """;
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
