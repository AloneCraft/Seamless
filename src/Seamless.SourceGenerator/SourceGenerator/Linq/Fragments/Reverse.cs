namespace Seamless.SourceGenerator.Linq.Fragments;

public class Reverse(int 番号, Enumerable source) : BufferedSequence(番号, source.ElementType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            while(source.MoveNext()) result.Add(source.Current);
            result.Reverse();
            return result;
        }
        """;
}
