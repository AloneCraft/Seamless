namespace Seamless.SourceGenerator.Linq.Fragments;

public class MinBy(int 番号, Enumerable source, FuncFragment keySelector, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
            if(!source.MoveNext()) throw global::Seamless.Linq.Enumerable.NoElements();
            {{resultType}} minElement = source.Current;
            {{FuncReturnTypeName(keySelector)}} minKey = default!;
            {{FuncAssign(keySelector, nameof(keySelector), "minKey", "minElement")}}
            var comparer = global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector, nameof(keySelector), key, item)}}
                if(comparer.Compare({{key}}, minKey) < 0) {
                    minElement = {{item}};
                    minKey = {{key}};
                }
            }
            return minElement;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
