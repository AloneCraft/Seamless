namespace Seamless.SourceGenerator.Linq.Fragments;

public class ToArray(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using var enumerator = source;
            while(enumerator.MoveNext()) result.Add(enumerator.Current);
            return result.ToArray();
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
