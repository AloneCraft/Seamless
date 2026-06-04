namespace Seamless.SourceGenerator.Linq.Fragments;

public class ElementAt_index(int 番号, Enumerable source, string index, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1
        && method.Parameters[0].Type is INamedTypeSymbol {
            Name: "Index",
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
        };
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {index})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, global::System.Index index";
    protected override string ComputeExpression => $"Compute({e}, index)";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, global::System.Index index) {
                source.TryGetSpan(out var __span);
                var idx=index.GetOffset(__span.Length);
                if((uint)idx>=(uint)__span.Length) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                return __span[idx];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, global::System.Index index) {
                if(!index.IsFromEnd) {
                    var idx=index.Value;
                    var currentIndex=0;
                    while(source.MoveNext()) {
                        if(currentIndex==idx) return source.Current;
                        currentIndex++;
                    }
                    throw new global::System.ArgumentOutOfRangeException(nameof(index));
                }else{
                    var n=index.Value;
                    if(n==0) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                    var count=0;
                    var buffer=new {{resultType}}[n];
                    while(source.MoveNext()) {
                        buffer[count%n]=source.Current;
                        count++;
                    }
                    if(count<n) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                    return buffer[(count-n)%n];
                }
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
