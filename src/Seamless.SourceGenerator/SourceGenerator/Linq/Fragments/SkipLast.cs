namespace Seamless.SourceGenerator.Linq.Fragments;

public class SkipLast(int 番号, Enumerable source, string count) : BufferedSequence(番号, source.ElementType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1 && method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {count})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source, int count";
    protected override string BuildExpression => "Build(source, count)";
    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, int count) {
            var result=new global::System.Collections.Generic.List<{{source.ElementType}}>();
            while(source.MoveNext()) {
                result.Add(source.Current);
            }
            var c=count<0?0:count;
            if(c>0&&result.Count>0) {
                var removeCount=global::System.Math.Min(c,result.Count);
                result.RemoveRange(result.Count-removeCount,removeCount);
            }
            return result;
        }
        """;
}
