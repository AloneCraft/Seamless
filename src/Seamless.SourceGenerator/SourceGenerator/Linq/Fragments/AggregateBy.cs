namespace Seamless.SourceGenerator.Linq.Fragments;
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : BufferedSequence(番号, resultElementType) {
    private string KeyType => FuncReturnTypeName(keySelector);
    private string AccumulatorType => resultElementType[resultElementType.IndexOf(',', System.StringComparison.Ordinal)..] is var _ ? FuncReturnTypeName(func) : FuncReturnTypeName(func);
    public override string StructMethod => $$"""
        {{(keySelector.CanInlineAssign ? string.Empty : this.DefineStruct(keySelector, nameof(keySelector)))}}
        {{(func.CanInlineAssign ? string.Empty : this.DefineStruct(func, nameof(func)))}}
        """;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => keySelector.CanInlineAssign && func.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(keySelector,nameof(keySelector))}, {this.FuncDelegate(func,nameof(func))})";
    public override string GetEnumeratorPrimaryConstructorParameters => keySelector.CanInlineAssign && func.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {this.KeyType}> keySelector, global::System.Func<{FuncParameterTypeName(func,0)}, {source.ElementType}, {FuncReturnTypeName(func)}> func";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{resultElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<{{resultElementType}}>();
            var map = new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                var {{this.key}} = {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}};
                if(!map.TryGetValue({{this.key}}, out var aggregate)) aggregate = {{seed}};
                aggregate = {{this.FuncInvoke(func,nameof(func),"aggregate",this.item)}};
                map[{{this.key}}] = aggregate;
            }
            foreach(var pair in map) result.Add(new {{resultElementType}}(pair.Key, pair.Value));
            return result;
        }
        """;
}
