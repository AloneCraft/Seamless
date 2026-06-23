namespace Seamless.FLinq.Linq.Fragments;
/*
MinMaxSelectorBase は source.Min(selector) / Max(selector) を selector 結果の最小/最大にする。

入力例:
  source.Max(x => x.Score)

生成例:
  if(!source.MoveNext()) throw NoElements();
  result = selector(source.Current);
  while(source.MoveNext()) {
      value = selector(source.Current);
      if(comparer.Compare(value, result) is better) result = value;
  }
  return result;

バリエーション:
  - source が TryGetSpan 可能なら span loop を試す。
  - direction により comparer.Compare(value, result) > 0 / < 0 を切り替える。
*/
public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(selector)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName = direction=="greater" ? "Max" : "Min";
        return RenderComputeInline(destVar,destType)
            ?? (TryRenderDirectLambda(selector) is { } lambda
                ? RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
                : null);
    }
    private string CompareCond => direction=="greater" ? "comparer.Compare(value,result)>0" : "comparer.Compare(value,result)<0";
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{resultType}} result=default!;
                if(source.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    var __first=true;
                    foreach(var {{item}} in __span){
                        {{resultType}} value=default!;
                        {{FuncAssignIndented("            ",selector,nameof(selector),"value",item)}}
                        if(__first||{{CompareCond}}){
                            __first=false;
                            result=value;
                        }
                    }
                }else{
                    {{source.ElementType}} {{item}};
                    if(!source.MoveNext()) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{item}}=source.Current;
                    {{FuncAssignIndented("        ",selector,nameof(selector),"result",item)}}
                    while(source.MoveNext()){
                        {{item}}=source.Current;
                        {{resultType}} value=default!;
                        {{FuncAssignIndented("            ",selector,nameof(selector),"value",item)}}
                        if({{CompareCond}}) result=value;
                    }
                }
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{source.ElementType}} {{item}};
                {{resultType}} result=default!;
                if(!source.MoveNext()) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{item}}=source.Current;
                {{FuncAssignIndented("    ",selector,nameof(selector),"result",item)}}
                while(source.MoveNext()){
                    {{item}}=source.Current;
                    {{resultType}} value=default!;
                    {{FuncAssignIndented("        ",selector,nameof(selector),"value",item)}}
                    if({{CompareCond}}) result=value;
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
