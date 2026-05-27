namespace Seamless.SourceGenerator.Linq.Fragments;

public class Distinct(int 番号, Enumerable source) : Distinct_comparer(番号, source, "null") { }

public class Distinct_comparer(int 番号, Enumerable source, string comparer) : BufferedSequence(番号, source.ElementType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string BuildExpression => "Build(source, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            while(source.MoveNext()) {
                if(seen.Add(source.Current)) result.Add(source.Current);
            }
            return result;
        }
        """;
}
