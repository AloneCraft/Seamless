namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy(int 番号, Enumerable source, FuncFragment keySelector) : BufferedSequence(番号, $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>") {
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
        private static global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}>> Build(TEnum source{{this.ComputeExtraParams(keySelector,KeySelectorFuncType,nameof(keySelector))}}) {
            var result = new global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}>>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>();
            while(source.MoveNext()) {
                var {{this.item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}} = default!;
                {{this.StaticComputeFuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
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
