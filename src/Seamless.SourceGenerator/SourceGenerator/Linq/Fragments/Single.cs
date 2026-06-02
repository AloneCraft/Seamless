namespace Seamless.SourceGenerator.Linq.Fragments;

public class Single(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            {{resultType}} value = source.Current;
            if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
            return value;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Single_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
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
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
            {{resultType}} value = default!;
            bool found = false;
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                bool {{temp}} = default!;
                {{FuncAssign(predicate, nameof(predicate), temp, item)}}
                if({{temp}}) {
                    if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                    value = {{item}};
                    found = true;
                }
            }
            if(!found) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            return value;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
