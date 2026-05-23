namespace Seamless.SourceGenerator.Linq.Fragments;
public class Contains(int 番号, Enumerable source, string value, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {value})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, {source.ElementType} value";
    protected override string ComputeExpression => "Compute(source, value)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, {{source.ElementType}} value) {
            var comparer = global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                if(comparer.Equals(e.Current, value)) return true;
            }
            return false;
        }
        """;
}
