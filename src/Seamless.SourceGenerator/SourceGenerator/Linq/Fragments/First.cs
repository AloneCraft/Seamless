namespace Seamless.SourceGenerator.Linq.Fragments;

public class First(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            if(source.MoveNext()) return source.Current;
            throw global::Seamless.Linq.Enumerable.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class First_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(predicate)})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                bool {{temp}} = default!;
                {{FuncAssign(predicate,nameof(predicate),temp,item)}}
                if({{temp}}) return {{item}};
            }
            throw global::Seamless.Linq.Enumerable.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
