namespace Seamless.SourceGenerator.Linq.Fragments;
public class CountBy(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.CountBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>? {{lookup}};
        private global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            if({{e}}.TryGetSpan(out var __span)) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>(__span.Length);
                this.{{group}}=new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__span.Length);
                for(var __i=0;__i<__span.Length;__i++) {
                    var {{item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookup}},{{key}},out var __exists);
                    if(!__exists) this.{{group}}.Add({{key}});
                    __cnt++;
                }
            }else{
                if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>(__count);
                    this.{{group}}=new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__count);
                }else{
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>();
                    this.{{group}}=new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
                }
                while({{e}}.MoveNext()) {
                    var {{item}}={{e}}.Current;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookup}},{{key}},out var __exists);
                    if(!__exists) this.{{group}}.Add({{key}});
                    __cnt++;
                }
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            var __key=this.{{group}}[this.{{a}}];
            current=this.{{currentValue}}=new global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>(__key,this.{{lookup}}[__key]);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>";
}
