namespace Seamless.SourceGenerator.Linq.Fragments;

public class Count(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            {{resultType}} count = 0;
            while(source.MoveNext()) count++;
            return count;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Count_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(predicate)})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
            {{resultType}} count = 0;
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                bool {{temp}} = default!;
                {{FuncAssign(predicate,nameof(predicate),temp,item)}}
                if({{temp}}) count++;
            }
            return count;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
