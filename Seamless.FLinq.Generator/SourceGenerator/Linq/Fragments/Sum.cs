namespace Seamless.FLinq.Generator;

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
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute(ref {this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeSumInline(destVar,destType)
        ??this.RenderDirectSpanSumInline(destVar,destType)
        ??this.RenderConsumeBasedSumInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"Sum")
        ??this.RenderComputeInline(destVar,destType);

    private string? RenderDirectArraySumInline(string destVar,string destType) {
        if(source.DirectArrayExpression is not { } arrayExpr) {
            return null;
        }
        if(this.CanUseSumSpan) {
            return $"{QueryProbeMarkers.Unknown}{destType} {destVar}=global::Seamless.Linq.Enumerable.SumSpan<{resultType}>({arrayExpr});";
        }
        var arrVar = $"__arr{番号}";
        var sumVar = $"__sum{番号}";
        var iVar = $"__i{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{arrVar}}={{arrayExpr}};
                {{resultType}} {{sumVar}}={{InitialValue(resultType)}};
                checked{
                    for(var {{iVar}}=0;{{iVar}}<{{arrVar}}.Length;{{iVar}}++) {
                        {{sumVar}}+={{Addend(resultType,arrVar+"["+iVar+"]")}};
                    }
                }
                {{destVar}}={{sumVar}};
            }
            """;
    }

    private string? RenderConsumeBasedSumInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }
        var placeholder = $"__FLINQ_SUM_{番号}__";
        string consumeStatement;
        try {
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
            return null;
        }
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            return $"{indent}{QueryProbeMarkers.Input}{destVar}+={Addend(resultType,arg)};";
        });
        return $$"""
            {{destType}} {{destVar}}={{InitialValue(resultType)}};
            {{replaced}}
            """;
    }
    // Range の和は和の公式で求める(checked の逐次加算と同じ桁あふれの判定つき)。
    private string? RenderRangeSumInline(string destVar,string destType) =>
        source is RangeRoot range&&resultType=="int"
            ? RangeArithmeticOptimizer.RenderInt32Sum(range,番号,destVar,destType)
            : null;
    // resultType==source.ElementType のときだけ SumSpan(INumber<T> 制約)へ委譲できる。
    // float は SumSpan 内部が checked スカラーループへ落ちて System.Linq の
    // 「double で蓄積してから float へ戻す」仕様と数値的に一致しなくなるため除外する。
    private bool CanUseSumSpan => resultType==source.ElementType&&IsKnownSimdSumType(source.ElementType);
    private string? RenderDirectSpanSumInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => this.CanUseSumSpan
            ? $$"""
                {{QueryProbeMarkers.Unknown}}{{destVar}}=global::Seamless.Linq.Enumerable.SumSpan<{{resultType}}>({{spanName}});
                """
            : $$"""
                {{resultType}} sum={{InitialValue(resultType)}};
                checked{
                    for(var __i=0;__i<{{spanName}}.Length;__i++) {{QueryProbeMarkers.Input}}sum+={{Addend(resultType,spanName+"[__i]")}};
                }
                {{destVar}}=sum;
                """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? (this.CanUseSumSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(ref TEnum source){
                    source.TryGetSpan(out var __span);
                    {{QueryProbeMarkers.Unknown}}return global::Seamless.Linq.Enumerable.SumSpan<{{resultType}}>(__span);
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(ref TEnum source){
                    {{resultType}} sum = {{InitialValue(resultType)}};
                    checked{
                        source.TryGetSpan(out var __span);
                        for(var __i=0;__i<__span.Length;__i++) {{QueryProbeMarkers.Input}}sum+={{Addend(resultType,"__span[__i]")}};
                    }
                    return sum;
                }
                """)
        : source.CanTryProvideSpan
        ? (this.CanUseSumSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(ref TEnum source){
                    if(source.TryGetSpan(out var __span)){
                        {{QueryProbeMarkers.Unknown}}return global::Seamless.Linq.Enumerable.SumSpan<{{resultType}}>(__span);
                    }
                    {{resultType}} sum = {{InitialValue(resultType)}};
                    checked{
                        while(source.TryGetNext(out var __mn71)) {{QueryProbeMarkers.Input}}sum+={{Addend(resultType,"__mn71")}};
                    }
                    return sum;
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(ref TEnum source){
                    {{resultType}} sum = {{InitialValue(resultType)}};
                    checked{
                        if(source.TryGetSpan(out var __span)){
                            for(var __i=0;__i<__span.Length;__i++) {{QueryProbeMarkers.Input}}sum+={{Addend(resultType,"__span[__i]")}};
                        }else{
                            while(source.TryGetNext(out var __mn71)) {{QueryProbeMarkers.Input}}sum+={{Addend(resultType,"__mn71")}};
                        }
                    }
                    return sum;
                }
                """)
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                {{resultType}} sum = {{InitialValue(resultType)}};
                checked{
                    while(source.TryGetNext(out var __mn72)) {{QueryProbeMarkers.Input}}sum+={{Addend(resultType,"__mn72")}};
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    // Nullable Sum returns a non-null zero for an empty or all-null input.
    // GetValueOrDefault reads each input once and keeps checked numeric addition.
    internal static string InitialValue(string typeName){if(IsNullableResult(typeName)){return "0";}return "default!";}
    internal static string Addend(string typeName,string expression){if(IsNullableResult(typeName)){return "("+expression+").GetValueOrDefault()";}return expression;}
    // 集計が値を1件受け取って足し込む文。計測点の目印(入力1件)を先頭に付ける(PipelineTerminal の全経路がこれを使う)。
    internal static string Accumulate(string typeName,string target,string expression)=>$"{QueryProbeMarkers.Input}checked {{ {target}+={Addend(typeName,expression)}; }}";
    private static bool IsNullableResult(string typeName)=>typeName.EndsWith("?",StringComparison.Ordinal)||typeName.StartsWith("global::System.Nullable<",StringComparison.Ordinal)||typeName.StartsWith("System.Nullable<",StringComparison.Ordinal);

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
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { selector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute(ref {this.e}{this.ClosureArgsSuffix(selector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeSumInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType)
        ??(TryRenderDirectLambda(selector) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Sum",lambda)
            : null);
    // Range の x・(long)x・(double)x の和は和の公式で求める。
    private string? RenderRangeSumInline(string destVar,string destType) {
        if(source is not RangeRoot range||!RangeArithmeticOptimizer.TryParseLinear(selector,out var linear)) {
            return null;
        }
        return (linear.Conversion,resultType) switch {
            ("long","long") => RangeArithmeticOptimizer.RenderInt64Sum(range,番号,destVar,destType),
            ("double","double") => RangeArithmeticOptimizer.RenderDoubleSum(range,番号,destVar,destType),
            (null,"int") when linear is { Scale: 1,Offset: 0 } => RangeArithmeticOptimizer.RenderInt32Sum(range,番号,destVar,destType),
            _ => null,
        };
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
                {{resultType}} sum = {{Sum.InitialValue(resultType)}};
                checked{
                    source.TryGetSpan(out var __span);
                    for(var __i=0;__i<__span.Length;__i++){
                        {{QueryProbeMarkers.Input}}var {{this.item}}=__span[__i];
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        // System.Linq は選択子を呼び出し側の文脈(通常は unchecked)で実行し、checked なのは足し算だけ。
                        // 選択子の中の縮小変換などを checked にすると OverflowException になり、System.Linq と食い違う。
                        unchecked{
                        {{this.FuncAssignIndented("                ",selector,nameof(selector),this.temp,this.item)}}
                        }
                        sum+={{Sum.Addend(resultType,this.temp)}};
                    }
                }
                return sum;
            }
            """
        : source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
                {{resultType}} sum = {{Sum.InitialValue(resultType)}};
                checked{
                    if(source.TryGetSpan(out var __span)){
                        for(var __i=0;__i<__span.Length;__i++){
                            {{QueryProbeMarkers.Input}}var {{this.item}}=__span[__i];
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            // System.Linq は選択子を呼び出し側の文脈(通常は unchecked)で実行し、checked なのは足し算だけ。
                            // 選択子の中の縮小変換などを checked にすると OverflowException になり、System.Linq と食い違う。
                            unchecked{
                            {{this.FuncAssignIndented("                ",selector,nameof(selector),this.temp,this.item)}}
                            }
                            sum+={{Sum.Addend(resultType,this.temp)}};
                        }
                    }else{
                        while(source.TryGetNext(out var __mn73)){
                            {{QueryProbeMarkers.Input}}var {{this.item}}=__mn73;
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            // System.Linq は選択子を呼び出し側の文脈(通常は unchecked)で実行し、checked なのは足し算だけ。
                            // 選択子の中の縮小変換などを checked にすると OverflowException になり、System.Linq と食い違う。
                            unchecked{
                            {{this.FuncAssignIndented("                ",selector,nameof(selector),this.temp,this.item)}}
                            }
                            sum+={{Sum.Addend(resultType,this.temp)}};
                        }
                    }
                }
                return sum;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
                {{resultType}} sum = {{Sum.InitialValue(resultType)}};
                checked{
                    while(source.TryGetNext(out var __mn74)){
                        {{QueryProbeMarkers.Input}}var {{this.item}}=__mn74;
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        // System.Linq は選択子を呼び出し側の文脈(通常は unchecked)で実行し、checked なのは足し算だけ。
                        // 選択子の中の縮小変換などを checked にすると OverflowException になり、System.Linq と食い違う。
                        unchecked{
                        {{this.FuncAssignIndented("            ",selector,nameof(selector),this.temp,this.item)}}
                        }
                        sum+={{Sum.Addend(resultType,this.temp)}};
                    }
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
