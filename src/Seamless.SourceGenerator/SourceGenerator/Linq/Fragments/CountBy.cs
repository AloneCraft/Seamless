namespace Seamless.SourceGenerator.Linq.Fragments;
public class CountBy(int 番号, Enumerable source, FuncFragment keySelector) : BufferedSequence(番号, $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>") {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(keySelector,nameof(keySelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{this.FuncPrimaryCtorExtra(keySelector,KeySelectorFuncType,nameof(keySelector))}";
    protected override string BuildExpression =>
        $"Build(source{this.ComputeExtraArgs(keySelector,nameof(keySelector))})";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>> Build(TEnum source{{this.ComputeExtraParams(keySelector,KeySelectorFuncType,nameof(keySelector))}}) {
            var result = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>();
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                {{this.StaticComputeFuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                map.TryGetValue({{this.key}}, out var count);
                map[{{this.key}}] = count + 1;
            }
            foreach(var pair in map) result.Add(pair);
            return result;
        }
        """;
}
