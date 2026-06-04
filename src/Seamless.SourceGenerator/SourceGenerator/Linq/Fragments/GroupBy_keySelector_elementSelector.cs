namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy_keySelector_elementSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector)
    : BufferedSequence(番号, $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {FuncReturnTypeName(elementSelector)}>") {

    // elementSelector は Func<TSource, TElement>（型引数2個）で識別する。
    // resultSelector Func<TKey, IEnumerable<TSource>, TResult>（型引数3個）は除外。
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 };

    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string StructMethod => JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector)));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    protected override string BuildExpression =>
        $"Build(source{ClosureArgsSuffix(keySelector, elementSelector)})";
    protected override string BuildMembers => $$"""
        private sealed class Grouping : global::System.Linq.IGrouping<{{KeyType}}, {{ElemType}}> {
            private readonly global::System.Collections.Generic.List<{{ElemType}}> values = new();
            public Grouping({{KeyType}} key) {
                this.Key = key;
            }
            public {{KeyType}} Key { get; }
            public void Add({{ElemType}} value) => values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{ElemType}}> GetEnumerator() => values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        private static global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{KeyType}}, {{ElemType}}>> Build(TEnum source{{ClosureMethodParameterSuffix(keySelector, elementSelector)}}) {
            var result = new global::System.Collections.Generic.List<global::System.Linq.IGrouping<{{KeyType}}, {{ElemType}}>>();
            var map = new global::System.Collections.Generic.Dictionary<{{KeyType}}, Grouping>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{KeyType}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!map.TryGetValue({{key}}, out var group)) {
                    group = new Grouping({{key}});
                    map.Add({{key}}, group);
                    result.Add(group);
                }
                {{ElemType}} {{a}} = default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),a,item)}}
                group.Add({{a}});
            }
            return result;
        }
        """;
}
