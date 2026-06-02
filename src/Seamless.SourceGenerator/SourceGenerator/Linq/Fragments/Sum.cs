namespace Seamless.SourceGenerator.Linq.Fragments;

public class Sum(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            {{resultType}} sum = default!;
            while(source.MoveNext()) sum += source.Current;
            return sum;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Sum_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(selector)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
            {{resultType}} sum = default!;
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(selector)}} {{temp}} = default!;
                {{FuncAssign(selector,nameof(selector),temp,item)}}
                sum += {{temp}};
            }
            return sum;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
