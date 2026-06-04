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
    protected override string BuildMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, int count) {
                var c=count<0?0:count;
                source.TryGetSpan(out var __span);
                var keep=__span.Length-global::System.Math.Min(c,__span.Length);
                var result=new global::System.Collections.Generic.List<{{source.ElementType}}>(keep);
                foreach(var item in __span.Slice(0,keep)) result.Add(item);
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, int count) {
                var c=count<0?0:count;
                var buffer=new global::System.Collections.Generic.List<{{source.ElementType}}>();
                while(source.MoveNext()) {
                    buffer.Add(source.Current);
                }
                if(c>0&&buffer.Count>0) {
                    var removeCount=global::System.Math.Min(c,buffer.Count);
                    buffer.RemoveRange(buffer.Count-removeCount,removeCount);
                }
                return buffer;
            }
            """;
}
