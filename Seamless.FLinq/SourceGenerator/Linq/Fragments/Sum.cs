namespace Seamless.FLinq.Linq.Fragments;

/*
Sum は source.Sum() / Sum(selector) を checked 加算の Compute に変換する。

入力例:
  source.Sum()
  source.Sum(x => x.Amount)

生成例:
  sum = default;
  checked {
      foreach(var item in source) sum += selectorOrItem(item);
  }
  return sum;

バリエーション:
  - source が TryGetSpan 可能なら span loop を優先する。
  - selector 付きは各 item を FuncAssign で一時値へ写してから加算する。
  - DirectEnumerableExpression + lambda 再表示可能なら System.Linq.Sum へ委譲できる。
*/
public class Sum(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderDirectEnumerableCallInline(source,destVar,destType,"Sum")
        ?? RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                {{resultType}} sum = default!;
                checked{
                    if(source.TryGetSpan(out var __span)){
                        foreach(var item in __span) sum+=item;
                    }else{
                        while(source.MoveNext()) sum+=source.Current;
                    }
                }
                return sum;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                {{resultType}} sum = default!;
                checked{
                    while(source.MoveNext()) sum+=source.Current;
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Sum_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
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
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderComputeInline(destVar,destType)
        ?? (TryRenderDirectLambda(selector) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"Sum",lambda)
            : null);
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                {{resultType}} sum = default!;
                checked{
                    if(source.TryGetSpan(out var __span)){
                        foreach(var {{item}} in __span){
                            {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                            {{FuncAssignIndented("                ",selector,nameof(selector),temp,item)}}
                            sum+={{temp}};
                        }
                    }else{
                        while(source.MoveNext()){
                            var {{item}}=source.Current;
                            {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                            {{FuncAssignIndented("                ",selector,nameof(selector),temp,item)}}
                            sum+={{temp}};
                        }
                    }
                }
                return sum;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                {{resultType}} sum = default!;
                checked{
                    while(source.MoveNext()){
                        var {{item}}=source.Current;
                        {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                        {{FuncAssignIndented("            ",selector,nameof(selector),temp,item)}}
                        sum+={{temp}};
                    }
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
