namespace Seamless.SourceGenerator.Linq.Fragments;

public class ToArray(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) result.Add(e.Current);
            return ({{resultType}})(object)result.ToArray();
        }
        """;
}

public class ToList(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) result.Add(e.Current);
            return ({{resultType}})(object)result;
        }
        """;
}

public class SequenceEqual(int 番号, Enumerable source, Enumerable second, string resultType) : SequenceEqual_comparer(番号, source, second, "null", resultType) { }

public class SequenceEqual_comparer(int 番号, Enumerable source, Enumerable second, string comparer, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => "Compute(source, second, comparer)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            comparer ??= global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            using var left = source.GetEnumerator();
            using var right = second.GetEnumerator();
            while(true) {
                var hasLeft = left.MoveNext();
                var hasRight = right.MoveNext();
                if(hasLeft != hasRight) return false;
                if(!hasLeft) return true;
                if(!comparer.Equals(left.Current, ({{source.ElementType}})(object)right.Current!)) return false;
            }
        }
        """;
}
