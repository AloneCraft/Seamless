namespace Seamless.SourceGenerator.Linq.Fragments;
public class Prepend(int 番号, Enumerable source, string element) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {element})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, {source.ElementType} element";
    protected override string BuildExpression => "Build(source, element)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, {{source.ElementType}} element) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}> { element };
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                result.Add(e.Current);
            }
            return result;
        }
        """;
}
