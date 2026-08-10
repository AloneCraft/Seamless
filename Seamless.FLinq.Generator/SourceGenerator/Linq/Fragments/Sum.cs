namespace Seamless.FLinq;

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
  - DirectEnumerableExpression + lambda 再表示可能なら RenderDirectEnumerableCallInline 経由で
    BufferedScalar の Compute インライン展開に乗る(唯一の override である BufferedScalar.cs は
    methodName を無視して FLinq 自身の Compute を埋め込むため、System.Linq への委譲ではない)。
*/
internal class Sum(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

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
    // resultType==source.ElementType のときだけ SumSpan(INumber<T> 制約)へ委譲できる。
    // float は SumSpan 内部が checked スカラーループへ落ちて System.Linq の
    // 「double で蓄積してから float へ戻す」仕様と数値的に一致しなくなるため除外する。
    private bool CanUseSumSpan => resultType==source.ElementType&&IsKnownSimdSumType(source.ElementType);
    private string? RenderDirectSpanSumInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => this.CanUseSumSpan
            ? $$"""
                {{destVar}}=global::Seamless.Linq.Enumerable.SumSpan<{{resultType}}>({{spanName}});
                """
            : $$"""
                {{resultType}} sum=default!;
                checked{
                    for(var __i=0;__i<{{spanName}}.Length;__i++) sum+={{spanName}}[__i];
                }
                {{destVar}}=sum;
                """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? (this.CanUseSumSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source){
                    source.TryGetSpan(out var __span);
                    return global::Seamless.Linq.Enumerable.SumSpan<{{resultType}}>(__span);
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source){
                    {{resultType}} sum = default!;
                    checked{
                        source.TryGetSpan(out var __span);
                        for(var __i=0;__i<__span.Length;__i++) sum+=__span[__i];
                    }
                    return sum;
                }
                """)
        : source.CanTryProvideSpan
        ? (this.CanUseSumSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source){
                    if(source.TryGetSpan(out var __span)){
                        return global::Seamless.Linq.Enumerable.SumSpan<{{resultType}}>(__span);
                    }
                    {{resultType}} sum = default!;
                    checked{
                        while(source.TryGetNext(out var __mn71)) sum+=__mn71;
                    }
                    return sum;
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source){
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
                """)
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source){
                {{resultType}} sum = default!;
                checked{
                    while(source.TryGetNext(out var __mn72)) sum+=__mn72;
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    private static bool IsKnownSimdSumType(string typeName) =>
        // SumSpan(Seamless.Linq)の SIMD 対応型と一致させる。float は除外(コメント参照)。
        typeName is "byte" or "sbyte"
            or "short" or "ushort"
            or "int" or "uint"
            or "long" or "ulong"
            or "double" or "decimal"
            or "global::System.Byte" or "global::System.SByte"
            or "global::System.Int16" or "global::System.UInt16"
            or "global::System.Int32" or "global::System.UInt32"
            or "global::System.Int64" or "global::System.UInt64"
            or "global::System.Double" or "global::System.Decimal";
}

internal class Sum_selector(int 番号,Enumerable source,FuncFragment selector,string resultType):BufferedScalar(番号,resultType) {
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
        if(selector is { } __visitorChild_selector) {
            yield return __visitorChild_selector;
        }
    }

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
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
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
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
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
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
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
