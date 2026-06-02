namespace Seamless.SourceGenerator.Linq.Fragments;

public class Average(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            long count = 0;
            double sum = 0;
            while(source.MoveNext()) {
                sum += global::System.Convert.ToDouble(source.Current);
                count++;
            }
            if(count == 0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            return ({{resultType}})(sum / count);
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Average_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
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
            long count = 0;
            double sum = 0;
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(selector)}} {{temp}} = default!;
                {{FuncAssign(selector,nameof(selector),temp,item)}}
                sum += global::System.Convert.ToDouble({{temp}});
                count++;
            }
            if(count == 0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            return ({{resultType}})(sum / count);
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
