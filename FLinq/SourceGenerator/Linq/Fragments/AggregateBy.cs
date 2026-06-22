namespace Seamless.FLinq.Linq.Fragments;
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);

    public override string StructMethod => $$"""
        {{this.FuncSupport(keySelector,nameof(keySelector))}}
        {{this.FuncSupport(func,nameof(func))}}
        """;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Collections.Tsavorite.Carrier.ValueEnumerableExtensions.AggregateBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {seed}, {func.DelegateConstructorArg ?? func.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector,func)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,func)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>? {{this.lookup}};
        private global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>.Enumerator {{this.innerEnumerator}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.lookup}} is null) {
            if({{this.e}}.TryGetSpan(out var __span)) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>(__span.Length);
                for(var __i=0;__i<__span.Length;__i++) {
                    var {{this.item}}=__span[__i];
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }else{
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>(__count);
                }else{
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>();
                }
                while({{this.e}}.MoveNext()) {
                    var {{this.item}}={{this.e}}.Current;
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }
            this.{{this.innerEnumerator}}=this.{{this.lookup}}.GetEnumerator();
        }
        if(this.{{this.innerEnumerator}}.MoveNext()) {
            var __kv=this.{{this.innerEnumerator}}.Current;
            current=this.{{this.currentValue}}=new {{resultElementType}}(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => resultElementType;
}
