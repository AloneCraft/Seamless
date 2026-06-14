namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Count(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) {
            return this.RenderComputeInline(destVar,destType);
        }
        if(resultType is not ("long" or "global::System.Int64")) {
            return this.RenderDirectEnumerableCallInline(source,destVar,destType,"Count");
        }
        var sourceVar=$"__src{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount({{sourceVar}}, out var __count)) {
                    {{destVar}}=({{resultType}})__count;
                }else{
                    {{destVar}}=global::System.Linq.Enumerable.LongCount({{sourceVar}});
                }
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return ({{resultType}})__span.Length;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                if(source.TryGetNonEnumeratedCount(out var __count)) {
                    return ({{resultType}})__count;
                }
                {{resultType}} count=0;
                checked{
                    while(source.MoveNext()) count++;
                }
                return count;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Count_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName = resultType is "long" or "global::System.Int64" ? "LongCount" : "Count";
        return TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
            : this.RenderComputeInline(destVar,destType);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                source.TryGetSpan(out var __span);
                {{resultType}} count = 0;
                checked{
                    for(var __i = 0; __i < __span.Length; __i++) {
                        var {{this.item}} = __span[__i];
                        bool {{this.temp}} = default!;
                        {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
                        if({{this.temp}}) count++;
                    }
                }
                return count;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} count = 0;
                checked{
                    while(source.MoveNext()) {
                        var {{this.item}} = source.Current;
                        bool {{this.temp}} = default!;
                        {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
                        if({{this.temp}}) count++;
                    }
                }
                return count;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
