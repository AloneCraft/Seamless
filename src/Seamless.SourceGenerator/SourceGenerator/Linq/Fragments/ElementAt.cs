namespace Seamless.SourceGenerator.Linq.Fragments;

public class ElementAt(int 番号, Enumerable source, string index, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {index})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, int index";
    protected override string ComputeExpression => "Compute(source, index)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, int index) {
            if(index < 0) throw new global::System.ArgumentOutOfRangeException(nameof(index));
            using var e = source.GetEnumerator();
            var currentIndex = 0;
            while(e.MoveNext()) {
                if(currentIndex == index) return ({{resultType}})(object)e.Current!;
                currentIndex++;
            }
            throw new global::System.ArgumentOutOfRangeException(nameof(index));
        }
        """;
}
