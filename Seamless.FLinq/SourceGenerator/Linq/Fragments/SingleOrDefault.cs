namespace Seamless.FLinq.Linq.Fragments;

public class SingleOrDefault(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingleOrDefault(range,番号,destVar,destType)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderSingleOrDefault(repeat,番号,destVar,destType)
            : RenderDirectSpanSingleOrDefaultInline(destVar,destType)
            ?? RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault")
        ?? RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanSingleOrDefaultInline(string destVar,string destType) {
        var span=$"__span{result}";
        return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            if({{spanName}}.Length==0) {
                {{destVar}}=default!;
            } else {
                if({{spanName}}.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                {{destVar}}={{spanName}}[0];
            }
            """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) return default!;
                if(__span.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return __span[0];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                if(!source.MoveNext()) return default!;
                {{resultType}} value = source.Current;
                if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class SingleOrDefault_defaultValue(int 番号, Enumerable source, string defaultValue, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind != TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {resultType} defaultValue";
    protected override string ComputeExpression => $"Compute({e}, defaultValue)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingleOrDefault(range,番号,destVar,destType,defaultValue)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderSingleOrDefault(repeat,番号,destVar,destType,defaultValue)
            : RenderDirectSpanSingleOrDefaultDefaultInline(destVar,destType)
            ?? RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault",defaultValue)
        ?? RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanSingleOrDefaultDefaultInline(string destVar,string destType) {
        var span=$"__span{result}";
        return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            if({{spanName}}.Length==0) {
                {{destVar}}={{defaultValue}};
            } else {
                if({{spanName}}.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                {{destVar}}={{spanName}}[0];
            }
            """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{resultType}} defaultValue) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) return defaultValue;
                if(__span.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return __span[0];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{resultType}} defaultValue) {
                if(!source.MoveNext()) return defaultValue;
                {{resultType}} value = source.Current;
                if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class SingleOrDefault_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate, nameof(predicate));
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
            ? RangePredicateOptimizer.RenderSingleOrDefault(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault",rangeLambda)
                    : RenderComputeInline(destVar,destType))
        : (TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault",lambda)
            : RenderComputeInline(destVar,destType));
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span){
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate, nameof(predicate),temp,item)}}
                    if({{temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value={{item}};
                        found=true;
                    }
                }
                return value;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                while(source.MoveNext()) {
                    var {{item}} = source.Current;
                    bool {{temp}} = default!;
                    {{FuncAssignStaticIndented("        ",predicate, nameof(predicate),temp,item)}}
                    if({{temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value = {{item}};
                        found = true;
                    }
                }
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
