namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy_keySelector(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => JoinStaticMethods(FuncSupport(keySelector,nameof(keySelector)),$$"""
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
        """);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>? {{lookup}};
        private global::System.Collections.Generic.List<Grouping>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{lookup}} is null) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>();
                this.{{group}}=new global::System.Collections.Generic.List<Grouping>();
                {{e}}.TryGetSpan(out var __span);
                foreach(var {{item}} in __span){
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(!this.{{lookup}}.TryGetValue({{key}},out var __group)){
                        __group=new Grouping({{key}});
                        this.{{lookup}}.Add({{key}},__group);
                        this.{{group}}.Add(__group);
                    }
                    __group.Add({{item}});
                }
            }
            this.{{a}}++;
            if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
                current=this.{{currentValue}}=this.{{group}}[this.{{a}}];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{lookup}} is null) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>();
                this.{{group}}=new global::System.Collections.Generic.List<Grouping>();
                while({{e}}.MoveNext()) {
                    var {{item}}={{e}}.Current;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(!this.{{lookup}}.TryGetValue({{key}},out var __group)){
                        __group=new Grouping({{key}});
                        this.{{lookup}}.Add({{key}},__group);
                        this.{{group}}.Add(__group);
                    }
                    __group.Add({{item}});
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
    public override string ElementType => $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>";
}
