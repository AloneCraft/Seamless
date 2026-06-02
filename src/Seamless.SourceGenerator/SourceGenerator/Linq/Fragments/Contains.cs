namespace Seamless.SourceGenerator.Linq.Fragments;
public class Contains(int 番号, Enumerable source, string value, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {value})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {source.ElementType} value";
    protected override string ComputeExpression => $"Compute({e}, value)";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value) {
            var comparer = global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            while(source.MoveNext()) {
                if(comparer.Equals(source.Current, value)) return true;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Contains_comparer(int 番号, Enumerable source, string value, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {value}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {source.ElementType} value, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => $"Compute({e}, value, comparer)";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            comparer ??= global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            while(source.MoveNext()) {
                if(comparer.Equals(source.Current, value)) return true;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
