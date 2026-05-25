namespace Seamless.SourceGenerator.Linq.Fragments;
public class ElementAtOrDefault(int 番号, Enumerable source, string index, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {index})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int index";
    protected override string ComputeExpression => $"Compute({this.e}, index)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source, int index) {
            if(index < 0) return default!;
            var currentIndex = 0;
            while(source.MoveNext()) {
                if(currentIndex == index) return ({{resultType}})(object)source.Current!;
                currentIndex++;
            }
            return default!;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
