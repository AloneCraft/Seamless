namespace Seamless.SourceGenerator.Linq.Fragments;

public class Concat(int 番号, Enumerable source, Enumerable second) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    protected override string BuildExpression => "Build(source, second)";
    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum1 source, TEnum2 second) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            while(source.MoveNext()) result.Add(source.Current);
            while(second.MoveNext()) result.Add(second.Current);
            return result;
        }
        """;
}
