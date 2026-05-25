namespace Seamless.SourceGenerator.Linq.Fragments;
public class Contains(int 番号, Enumerable source, string value, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {value})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {source.ElementType} value";
    protected override string ComputeExpression => $"Compute({this.e}, value)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value) {
            var comparer = global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            while(source.MoveNext()) {
                if(comparer.Equals(source.Current, value)) return true;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
