namespace Seamless.SourceGenerator.Linq.Fragments;
public class Last(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source) {
            var found = false;
            {{resultType}} result = default!;
            while(source.MoveNext()) {
                found = true;
                result = source.Current;
            }
            if(!found) throw global::Seamless.Linq.Enumerable.NoElements();
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Last_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.FuncPrimaryCtorExtra(predicate,PredicateFuncType,nameof(predicate))}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ComputeExtraArgs(predicate,nameof(predicate))})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{this.ComputeExtraParams(predicate,PredicateFuncType,nameof(predicate))}}) {
            var found = false;
            {{resultType}} result = default!;
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                bool {{this.temp}} = default!;
                {{this.StaticComputeFuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
                if({{this.temp}}) {
                    found = true;
                    result = {{this.item}};
                }
            }
            if(!found) throw global::Seamless.Linq.Enumerable.NoElements();
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
