namespace Seamless.SourceGenerator.Linq.Fragments;

public class Chunk(int 番号, Enumerable source, string size) : BufferedSequence(番号, $"{source.ElementType}[]") {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {size})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source, int size";
    protected override string BuildExpression => "Build(source, size)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}[]> Build(TEnum source, int size) {
            if(size <= 0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}[]>();
            var chunk = new global::System.Collections.Generic.List<{{source.ElementType}}>(size);
            while(source.MoveNext()) {
                chunk.Add(source.Current);
                if(chunk.Count == size) {
                    result.Add(chunk.ToArray());
                    chunk.Clear();
                }
            }
            if(chunk.Count > 0) result.Add(chunk.ToArray());
            return result;
        }
        """;
}
