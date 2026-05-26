namespace Seamless.SourceGenerator.Linq.Fragments;

public class Sum(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            {{resultType}} sum = default!;
            while(source.MoveNext()) sum += source.Current;
            return sum;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Sum_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.FuncPrimaryCtorExtra(selector,SelectorFuncType,nameof(selector))}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ComputeExtraArgs(selector,nameof(selector))})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{this.ComputeExtraParams(selector,SelectorFuncType,nameof(selector))}}) {
            {{resultType}} sum = default!;
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(selector)}} {{this.temp}} = default!;
                {{this.StaticComputeFuncAssign(selector,nameof(selector),this.temp,this.item)}}
                sum += {{this.temp}};
            }
            return sum;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
