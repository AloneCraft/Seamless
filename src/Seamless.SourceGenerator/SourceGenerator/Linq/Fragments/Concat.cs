namespace Seamless.SourceGenerator.Linq.Fragments;

public class Concat(int 番号, Enumerable source, Enumerable second) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using(var e = source.GetEnumerator()) {
                while(e.MoveNext()) result.Add(e.Current);
            }
            using(var e = second.GetEnumerator()) {
                while(e.MoveNext()) result.Add(({{source.ElementType}})(object)e.Current!);
            }
            return result;
        }
        """;
}
