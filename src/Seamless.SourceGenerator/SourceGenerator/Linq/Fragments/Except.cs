namespace Seamless.SourceGenerator.Linq.Fragments;

public class Except(int 番号, Enumerable source, Enumerable second) : Except_comparer(番号, source, second, "null") { }

public class Except_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string BuildExpression => "Build(source, second, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var excluded = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            while(second.MoveNext()) excluded.Add(({{source.ElementType}})(object)second.Current!);
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var yielded = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            while(source.MoveNext()) if(!excluded.Contains(source.Current) && yielded.Add(source.Current)) result.Add(source.Current);
            return result;
        }
        """;
}
