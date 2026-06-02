namespace Seamless.SourceGenerator.Linq.Fragments;

public class ToHashSet(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
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
            var result = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>();
            while(source.MoveNext()) result.Add(source.Current);
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}

public class ToHashSet_comparer(int 番号, Enumerable source, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Name == "comparer";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => $"Compute({e}, comparer)";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var result = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            while(source.MoveNext()) result.Add(source.Current);
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
