namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy_keySelector_elementSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector)
    : Enumerable(番号) {

    // elementSelector は Func<TSource, TElement>（型引数2個）で識別する。
    // resultSelector Func<TKey, IEnumerable<TSource>, TResult>（型引数3個）は除外。
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 };

    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))),$$"""
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
        """);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, Grouping>? {{lookup}};
        private global::System.Collections.Generic.List<Grouping>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, Grouping>();
            this.{{group}}=new global::System.Collections.Generic.List<Grouping>();
            while({{e}}.MoveNext()) {
                var {{item}}={{e}}.Current;
                {{KeyType}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!this.{{lookup}}.TryGetValue({{key}},out var __group)) {
                    __group=new Grouping({{key}});
                    this.{{lookup}}.Add({{key}},__group);
                    this.{{group}}.Add(__group);
                }
                {{ElemType}} {{a}}=default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),a,item)}}
                __group.Add({{a}});
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            current=this.{{currentValue}}=this.{{group}}[this.{{a}}];
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {FuncReturnTypeName(elementSelector)}>";
}
