namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy(int 番号, Enumerable source, FuncFragment keySelector) : BufferedSequence(番号, $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>") {
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
        private static global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}>> Build(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
            var result = new global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}>>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!map.TryGetValue({{key}}, out var group)) {
                    group = new Grouping({{key}});
                    map.Add({{key}}, group);
                    result.Add(group);
                }
                group.Add({{item}});
            }
            return result;
        }
        """;
}
