namespace Seamless.SourceGenerator.Linq.Fragments;
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : BufferedSequence(番号, resultElementType) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);

    public override string StructMethod => $$"""
        {{FuncSupport(keySelector,nameof(keySelector))}}
        {{FuncSupport(func,nameof(func))}}
        """;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector,func)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{ClosurePrimaryCtorSuffix(keySelector,func)}";
    protected override string BuildExpression =>
        $"Build(source{ClosureArgsSuffix(keySelector,func)})";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{resultElementType}}> Build(TEnum source{{ClosureMethodParameterSuffix(keySelector,func)}}) {
            var result = new global::System.Collections.Generic.List<{{resultElementType}}>();
            var map = new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{KeyType}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!map.TryGetValue({{key}}, out var aggregate)) aggregate = {{seed}};
                {{FuncAssign(func,nameof(func),"aggregate","aggregate",item)}}
                map[{{key}}] = aggregate;
            }
            foreach(var pair in map) result.Add(new {{resultElementType}}(pair.Key, pair.Value));
            return result;
        }
        """;
}
