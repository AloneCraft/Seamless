namespace Seamless.SourceGenerator.Linq.Fragments;

public class DistinctBy(int 番号, Enumerable source, FuncFragment keySelector) : DistinctBy_comparer(番号, source, keySelector, "null") { }

public class DistinctBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : BufferedSequence(番号, source.ElementType) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string BuildExpression =>
        $"Build(source, comparer{ClosureArgsSuffix(keySelector)})";
    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(seen.Add({{key}})) result.Add({{item}});
            }
            return result;
        }
        """;
}
