namespace Seamless.SourceGenerator.Linq.Fragments;
public class CountBy(int 番号, Enumerable source, FuncFragment keySelector) : BufferedSequence(番号, $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>") {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string BuildExpression =>
        $"Build(source{ClosureArgsSuffix(keySelector)})";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>> Build(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
            var result = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                map.TryGetValue({{key}}, out var count);
                map[{{key}}] = count + 1;
            }
            foreach(var pair in map) result.Add(pair);
            return result;
        }
        """;
}
