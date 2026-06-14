namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Sum(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectEnumerableCallInline(source,destVar,destType,"Sum")
        ?? this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                {{resultType}} sum = default!;
                source.TryGetSpan(out var __span);
                checked{
                    foreach(var item in __span) sum+=item;
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
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Sum_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
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
        this.RenderRangeLongSumSelectorInline(destVar,destType)
        ?? (TryRenderDirectLambda(selector) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Sum",lambda)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderRangeLongSumSelectorInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !IsLongType(resultType)
            || !RangePredicateOptimizer.IsParameterCast(selector,"long")) {
            return null;
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var sum=$"__sum{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{sum}}=((long){{count}} * (2L * (long){{start}} + (long){{count}} - 1L)) / 2L;
                {{destVar}}=({{destType}}){{sum}};
            }
            """;
    }
    private static bool IsLongType(string typeName) =>
        typeName is "long" or "global::System.Int64" or "System.Int64";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                {{resultType}} sum = default!;
                source.TryGetSpan(out var __span);
                checked{
                    foreach(var {{this.item}} in __span){
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        {{this.FuncAssign(selector,nameof(selector),this.temp,this.item)}}
                        sum+={{this.temp}};
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
                    while(source.MoveNext()){
                        var {{this.item}}=source.Current;
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        {{this.FuncAssign(selector,nameof(selector),this.temp,this.item)}}
                        sum+={{this.temp}};
                    }
                }
                return sum;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
