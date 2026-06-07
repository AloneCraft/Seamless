namespace Seamless.SourceGenerator.Linq.Fragments;

public class ToList(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            global::System.Collections.Generic.List<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result=new(__count);
            }else{
                result=new();
            }
            using var enumerator = source;
            while(enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
