namespace Seamless.FLinq.Linq.Fragments;

public class SingleOrDefault(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingleOrDefault(range,番号,destVar,destType)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderSingleOrDefault(repeat,番号,destVar,destType)
            : this.RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault")
        ?? this.RenderComputeInline(destVar,destType);
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
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class SingleOrDefault_defaultValue(int 番号, Enumerable source, string defaultValue, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind != TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {resultType} defaultValue";
    protected override string ComputeExpression => $"Compute({this.e}, defaultValue)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingleOrDefault(range,番号,destVar,destType,defaultValue)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderSingleOrDefault(repeat,番号,destVar,destType,defaultValue)
            : this.RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault",defaultValue)
        ?? this.RenderComputeInline(destVar,destType);
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
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class SingleOrDefault_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate, nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingleOrDefault(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault",rangeLambda)
                    : this.RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"SingleOrDefault",lambda)
            : this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate, nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value={{this.item}};
                        found=true;
                    }
                }
                return value;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                while(source.MoveNext()) {
                    var {{this.item}} = source.Current;
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate, nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value = {{this.item}};
                        found = true;
                    }
                }
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
