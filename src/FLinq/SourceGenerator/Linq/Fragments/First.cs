namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class First(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderFirst(range,番号,destVar,destType)
        : this.RenderDirectEnumerableCallInline(source,destVar,destType,"First")
        ?? this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            if(source.MoveNext()) return source.Current;
            throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class First_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
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
    public override string? RenderInlineComputation(string destVar,string destType) =>
        TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"First",lambda)
            : this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                bool {{this.temp}} = default!;
                {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
                if({{this.temp}}) return {{this.item}};
            }
            throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
