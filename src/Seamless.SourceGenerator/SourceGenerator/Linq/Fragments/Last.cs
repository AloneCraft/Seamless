namespace Seamless.SourceGenerator.Linq.Fragments;
public class Last(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            using var e = source.GetEnumerator();
            var found = false;
            {{resultType}} result = default!;
            while(e.MoveNext()) {
                found = true;
                result = ({{resultType}})(object)e.Current!;
            }
            if(!found) throw global::Seamless.Linq.Enumerable.NoElements();
            return result;
        }
        """;
}

public class Last_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => predicate.CanInlineAssign ? string.Empty : this.DefineStruct(predicate, nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => predicate.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters => predicate.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, bool> predicate";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            using var e = source.GetEnumerator();
            var found = false;
            {{resultType}} result = default!;
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                if({{this.FuncInvoke(predicate,nameof(predicate),this.item)}}) {
                    found = true;
                    result = ({{resultType}})(object){{this.item}}!;
                }
            }
            if(!found) throw global::Seamless.Linq.Enumerable.NoElements();
            return result;
        }
        """;
}
