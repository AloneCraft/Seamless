namespace Seamless.SourceGenerator.Linq.Fragments;

public class First(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            if(source.MoveNext()) return source.Current;
            throw global::Seamless.Linq.Enumerable.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class First_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.FuncPrimaryCtorExtra(predicate,PredicateFuncType,nameof(predicate))}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ComputeExtraArgs(predicate,nameof(predicate))})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{this.ComputeExtraParams(predicate,PredicateFuncType,nameof(predicate))}}) {
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                if({{this.StaticComputeFuncInvoke(predicate,nameof(predicate),this.item)}}) return {{this.item}};
            }
            throw global::Seamless.Linq.Enumerable.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
