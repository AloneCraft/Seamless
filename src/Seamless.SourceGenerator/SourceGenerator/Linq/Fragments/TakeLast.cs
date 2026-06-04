namespace Seamless.SourceGenerator.Linq.Fragments;

public class TakeLast(int 番号, Enumerable source, string count) : BufferedSequence(番号, source.ElementType) {
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
                var start=__span.Length-global::System.Math.Min(c,__span.Length);
                var result=new global::System.Collections.Generic.List<{{source.ElementType}}>(__span.Length-start);
                foreach(var item in __span.Slice(start)) result.Add(item);
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
                var s=buffer.Count-global::System.Math.Min(c,buffer.Count);
                return buffer.GetRange(s,buffer.Count-s);
            }
            """;
}
