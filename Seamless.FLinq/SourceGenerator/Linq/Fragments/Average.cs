namespace Seamless.FLinq.Linq.Fragments;

/*
Average は source.Average() / Average(selector) を、sum と count を持つ Compute に変換する。

入力例:
  source.Average()
  source.Average(x => x.Score)

生成例:
  count = 0;
  sum = accumulatorDefault;
  foreach(var item in source) {
      sum += selectorOrItem(item);
      count++;
  }
  if(count == 0) throw NoElements();
  return sum / count;

バリエーション:
  - decimal は decimal accumulator、int/long/float/double は double accumulator を使う。
  - int の span source は AverageIntSimd を使う。
  - selector 付きは selector の戻り値型で accumulator 方針を決める。
*/
public class Average(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    private bool UseDecimalAccumulator => source.ElementType=="decimal";
    private bool UseDoubleAccumulator =>
        source.ElementType=="int"
        || source.ElementType=="long"
        || source.ElementType=="float"
        || source.ElementType=="double";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderDirectSpanAverageInline(destVar,destType)
        ?? RenderDirectEnumerableCallInline(source,destVar,destType,"Average")
        ?? RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanAverageInline(string destVar,string destType) {
        var span=$"__span{result}";
        if(UseDecimalAccumulator) {
            return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
                if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                var sum=0m;
                foreach(var item in {{spanName}}) sum+=item;
                {{destVar}}=({{resultType}})(sum/{{spanName}}.Length);
                """);
        }
        if(UseDoubleAccumulator) {
            if(source.ElementType=="int") {
                return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
                    if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{destVar}}=({{resultType}})global::Seamless.System.Carrier.ValueEnumerableExtensions.AverageIntSimd({{spanName}});
                    """);
            }
            return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
                if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                double sum=0;
                foreach(var item in {{spanName}}) sum+=item;
                {{destVar}}=({{resultType}})(sum/{{spanName}}.Length);
                """);
        }
        return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{resultType}} sum=default!;
            foreach(var item in {{spanName}}) sum+=item;
            {{destVar}}=sum/({{resultType}}){{spanName}}.Length;
            """);
    }
    protected override string ComputeMembers => BuildComputeMembers(source.ElementType,"item","source.Current");
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";

    private string BuildComputeMembers(string valueType,string spanValue,string enumValue) {
        if(UseDecimalAccumulator) {
            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source) {
                        var sum=0m;
                        source.TryGetSpan(out var __span);
                        if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        foreach(var item in __span) sum+=item;
                        return ({{resultType}})(sum/__span.Length);
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source) {
                        long count=0;
                        var sum=0m;
                        while(source.MoveNext()){
                            sum+=source.Current;
                            count++;
                        }
                        if(count==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        if(UseDoubleAccumulator) {
            return source.CanProvideSpan
                ? source.ElementType=="int"
                    ? $$"""
                        {{AggressiveInliningAttribute}}
                        private static {{resultType}} Compute(TEnum source) {
                            source.TryGetSpan(out var __span);
                            if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                            return ({{resultType}})global::Seamless.System.Carrier.ValueEnumerableExtensions.AverageIntSimd(__span);
                        }
                        """
                    : $$"""
                        {{AggressiveInliningAttribute}}
                        private static {{resultType}} Compute(TEnum source) {
                            double sum=0;
                            source.TryGetSpan(out var __span);
                            if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                            foreach(var item in __span) sum+=item;
                            return ({{resultType}})(sum/__span.Length);
                        }
                        """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source) {
                        long count=0;
                        double sum=0;
                        while(source.MoveNext()){
                            sum+=source.Current;
                            count++;
                        }
                        if(count==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        return source.CanProvideSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source) {
                    long count=0;
                    {{resultType}} sum=default!;
                    source.TryGetSpan(out var __span);
                    if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    foreach(var item in __span){
                        sum+=item;
                        count++;
                    }
                    return sum/({{resultType}})count;
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source) {
                    long count=0;
                    {{resultType}} sum=default!;
                    while(source.MoveNext()){
                        sum+=source.Current;
                        count++;
                    }
                    if(count==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    return sum/({{resultType}})count;
                }
                """;
    }
}

public class Average_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    private bool UseDecimalAccumulator => FuncReturnTypeName(selector)=="decimal";
    private bool UseDoubleAccumulator =>
        FuncReturnTypeName(selector)=="int"
        || FuncReturnTypeName(selector)=="long"
        || FuncReturnTypeName(selector)=="float"
        || FuncReturnTypeName(selector)=="double";
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
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"Average",lambda)
            : null);
    protected override string ComputeMembers => BuildComputeMembers();
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";

    private string BuildComputeMembers() {
        if(UseDecimalAccumulator) {
            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                        var sum=0m;
                        source.TryGetSpan(out var __span);
                        if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        foreach(var {{item}} in __span){
                            {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                            {{FuncAssignStaticIndented("        ",selector,nameof(selector),temp,item)}}
                            sum+={{temp}};
                        }
                        return ({{resultType}})(sum/__span.Length);
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                        long count=0;
                        var sum=0m;
                        while(source.MoveNext()){
                            var {{item}}=source.Current;
                            {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                            {{FuncAssignStaticIndented("        ",selector,nameof(selector),temp,item)}}
                            sum+={{temp}};
                            count++;
                        }
                        if(count==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        if(UseDoubleAccumulator) {
            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                        double sum=0;
                        source.TryGetSpan(out var __span);
                        if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        foreach(var {{item}} in __span){
                            {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                            {{FuncAssignStaticIndented("        ",selector,nameof(selector),temp,item)}}
                            sum+={{temp}};
                        }
                        return ({{resultType}})(sum/__span.Length);
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                        long count=0;
                        double sum=0;
                        while(source.MoveNext()){
                            var {{item}}=source.Current;
                            {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                            {{FuncAssignStaticIndented("        ",selector,nameof(selector),temp,item)}}
                            sum+={{temp}};
                            count++;
                        }
                        if(count==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        return source.CanProvideSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                    long count=0;
                    {{resultType}} sum=default!;
                    source.TryGetSpan(out var __span);
                    if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    foreach(var {{item}} in __span){
                        {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                        {{FuncAssignStaticIndented("        ",selector,nameof(selector),temp,item)}}
                        sum+={{temp}};
                        count++;
                    }
                    return sum/({{resultType}})count;
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                    long count=0;
                    {{resultType}} sum=default!;
                    while(source.MoveNext()){
                        var {{item}}=source.Current;
                        {{FuncReturnTypeName(selector)}} {{temp}}=default!;
                        {{FuncAssignStaticIndented("        ",selector,nameof(selector),temp,item)}}
                        sum+={{temp}};
                        count++;
                    }
                    if(count==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    return sum/({{resultType}})count;
                }
                """;
    }
}
