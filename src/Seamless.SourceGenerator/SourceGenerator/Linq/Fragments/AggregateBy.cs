namespace Seamless.SourceGenerator.Linq.Fragments;
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : BufferedSequence(番号, resultElementType) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string AccumulatorType => resultElementType[resultElementType.IndexOf(',', System.StringComparison.Ordinal)..] is var _ ? FuncReturnTypeName(func) : FuncReturnTypeName(func);
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {this.KeyType}>";
    private string FuncType => $"global::System.Func<{FuncParameterTypeName(func,0)}, {source.ElementType}, {FuncReturnTypeName(func)}>";
    public override string StructMethod => $$"""
        {{this.FuncSupport(keySelector,nameof(keySelector))}}
        {{this.FuncSupport(func,nameof(func))}}
        """;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.FuncEnumeratorArgExtra(keySelector,nameof(keySelector))}{this.FuncEnumeratorArgExtra(func,nameof(func))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{this.FuncPrimaryCtorExtra(keySelector,KeySelectorFuncType,nameof(keySelector))}{this.FuncPrimaryCtorExtra(func,FuncType,nameof(func))}";
    protected override string BuildExpression =>
        $"Build(source{this.ComputeExtraArgs(keySelector,nameof(keySelector))}{this.ComputeExtraArgs(func,nameof(func))})";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{resultElementType}}> Build(TEnum source{{this.ComputeExtraParams(keySelector,KeySelectorFuncType,nameof(keySelector))}}{{this.ComputeExtraParams(func,FuncType,nameof(func))}}) {
            var result = new global::System.Collections.Generic.List<{{resultElementType}}>();
            var map = new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>();
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{this.KeyType}} {{this.key}} = default!;
                {{this.StaticComputeFuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                if(!map.TryGetValue({{this.key}}, out var aggregate)) aggregate = {{seed}};
                {{this.StaticComputeFuncAssign(func,nameof(func),"aggregate","aggregate",this.item)}}
                map[{{this.key}}] = aggregate;
            }
            foreach(var pair in map) result.Add(new {{resultElementType}}(pair.Key, pair.Value));
            return result;
        }
        """;
}
