namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy(int 番号, Enumerable source, FuncFragment keySelector) : BufferedSequence(番号, $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>") {
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
        private sealed class Grouping : global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}> {
            private readonly global::System.Collections.Generic.List<{{source.ElementType}}> values = new();
            public Grouping({{FuncReturnTypeName(keySelector)}} key) {
                this.Key = key;
            }
            public {{FuncReturnTypeName(keySelector)}} Key { get; }
            public void Add({{source.ElementType}} value) => values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> GetEnumerator() => values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private static global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}>> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}>>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                var {{this.key}} = {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}};
                if(!map.TryGetValue({{this.key}}, out var group)) {
                    group = new Grouping({{this.key}});
                    map.Add({{this.key}}, group);
                    result.Add(group);
                }
                group.Add({{this.item}});
            }
            return result;
        }
        """;
}
