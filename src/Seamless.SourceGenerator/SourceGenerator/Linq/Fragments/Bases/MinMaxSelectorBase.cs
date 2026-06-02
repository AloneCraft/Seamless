namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
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
            if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            var {{item}} = source.Current;
            {{resultType}} result = default!;
            {{FuncAssign(selector,nameof(selector),"result",item)}}
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(source.MoveNext()) {
                {{item}} = source.Current;
                {{resultType}} value = default!;
                {{FuncAssign(selector,nameof(selector),"value",item)}}
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
