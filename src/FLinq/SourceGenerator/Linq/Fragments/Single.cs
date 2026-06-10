namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Single(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                if(__span.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return __span[0];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                {{resultType}} value = source.Current;
                if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Single_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
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
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}}=default!;
                    {{this.FuncAssign(predicate, nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value={{this.item}};
                        found=true;
                    }
                }
                if(!found) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
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
                    {{this.FuncAssign(predicate, nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value = {{this.item}};
                        found = true;
                    }
                }
                if(!found) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
