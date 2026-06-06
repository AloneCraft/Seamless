namespace Seamless.SourceGenerator.Linq.Fragments;
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);

    public override string StructMethod => $$"""
        {{FuncSupport(keySelector,nameof(keySelector))}}
        {{FuncSupport(func,nameof(func))}}
        """;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.AggregateBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {seed}, {func.DelegateConstructorArg ?? func.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector,func)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector,func)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>? {{lookup}};
        private global::System.Collections.Generic.List<{{KeyType}}>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            if({{e}}.TryGetSpan(out var __span)) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>(__span.Length);
                this.{{group}}=new global::System.Collections.Generic.List<{{KeyType}}>(__span.Length);
                for(var __i=0;__i<__span.Length;__i++) {
                    var {{item}}=__span[__i];
                    {{KeyType}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookup}},{{key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                        this.{{group}}.Add({{key}});
                    }
                    {{FuncAssign(func,nameof(func),"__agg","__agg",item)}}
                }
            }else{
                if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>(__count);
                    this.{{group}}=new global::System.Collections.Generic.List<{{KeyType}}>(__count);
                }else{
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>();
                    this.{{group}}=new global::System.Collections.Generic.List<{{KeyType}}>();
                }
                while({{e}}.MoveNext()) {
                    var {{item}}={{e}}.Current;
                    {{KeyType}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookup}},{{key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                        this.{{group}}.Add({{key}});
                    }
                    {{FuncAssign(func,nameof(func),"__agg","__agg",item)}}
                }
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            var __key=this.{{group}}[this.{{a}}];
            current=this.{{currentValue}}=new {{resultElementType}}(__key,this.{{lookup}}[__key]);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => resultElementType;
}
