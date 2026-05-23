namespace Seamless.SourceGenerator.Linq.Fragments;
public class CountBy(int 番号, Enumerable source, FuncFragment keySelector) : BufferedSequence(番号, $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>") {
    public override string StructMethod => keySelector.CanInlineAssign ? string.Empty : this.DefineStruct(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => keySelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(keySelector,nameof(keySelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => keySelector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}> keySelector";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                var {{this.key}} = {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}};
                map.TryGetValue({{this.key}}, out var count);
                map[{{this.key}}] = count + 1;
            }
            foreach(var pair in map) result.Add(pair);
            return result;
        }
        """;
}
