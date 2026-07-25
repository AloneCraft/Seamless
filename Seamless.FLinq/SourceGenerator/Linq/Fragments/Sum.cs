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
public class Sum(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectSpanSumInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"Sum")
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanSumInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{resultType}} sum=default!;
            checked{
                for(var __i=0;__i<{{spanName}}.Length;__i++) sum+={{spanName}}[__i];
            }
            {{destVar}}=sum;
            """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                {{resultType}} sum = default!;
                checked{
                    source.TryGetSpan(out var __span);
                    for(var __i=0;__i<__span.Length;__i++) sum+=__span[__i];
                }
                return sum;
            }
            """
        : source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                {{resultType}} sum = default!;
                checked{
                    if(source.TryGetSpan(out var __span)){
                        for(var __i=0;__i<__span.Length;__i++) sum+=__span[__i];
                    }else{
                        while(source.TryGetNext(out var __mn71)) sum+=__mn71;
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
                    while(source.TryGetNext(out var __mn72)) sum+=__mn72;
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Sum_selector(int 番号,Enumerable source,FuncFragment selector,string resultType):BufferedScalar(番号,resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(selector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderComputeInline(destVar,destType)
        ??(TryRenderDirectLambda(selector) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Sum",lambda)
            : null);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                {{resultType}} sum = default!;
                checked{
                    source.TryGetSpan(out var __span);
                    for(var __i=0;__i<__span.Length;__i++){
                        var {{this.item}}=__span[__i];
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        {{this.FuncAssignIndented("                ",selector,nameof(selector),this.temp,this.item)}}
                        sum+={{this.temp}};
                    }
                }
                return sum;
            }
            """
        : source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                {{resultType}} sum = default!;
                checked{
                    if(source.TryGetSpan(out var __span)){
                        for(var __i=0;__i<__span.Length;__i++){
                            var {{this.item}}=__span[__i];
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            {{this.FuncAssignIndented("                ",selector,nameof(selector),this.temp,this.item)}}
                            sum+={{this.temp}};
                        }
                    }else{
                        while(source.TryGetNext(out var __mn73)){
                            var {{this.item}}=__mn73;
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            {{this.FuncAssignIndented("                ",selector,nameof(selector),this.temp,this.item)}}
                            sum+={{this.temp}};
                        }
                    }
                }
                return sum;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                {{resultType}} sum = default!;
                checked{
                    while(source.TryGetNext(out var __mn74)){
                        var {{this.item}}=__mn74;
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        {{this.FuncAssignIndented("            ",selector,nameof(selector),this.temp,this.item)}}
                        sum+={{this.temp}};
                    }
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
