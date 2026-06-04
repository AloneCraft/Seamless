namespace Seamless.SourceGenerator.Linq.Fragments;
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct(source.ElementType)}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            {{e}}.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            {{source.ElementType}} {{result}}=default!;
            var __first=true;
            foreach(var __item in __span){
                if(__first){
                    {{result}}=__item;
                    __first=false;
                }else{
                    {{FuncAssign(func,nameof(func),result,result,"__item")}}
                }
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """
        : $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            if(!{{e}}.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            var {{result}}={{e}}.Current;
            while({{e}}.MoveNext()){
                {{FuncAssign(func,nameof(func),result,result,$"{e}.Current")}}
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Aggregate_seed_func(int 番号, Enumerable source,string seed, FuncFragment func) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct(FuncParameterTypeName(func,0))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            {{e}}.TryGetSpan(out var __span);
            foreach(var __item in __span){
                {{FuncAssign(func,nameof(func),result,result,"__item")}}
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """
        : $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            while({{e}}.MoveNext()){
                {{FuncAssign(func,nameof(func),result,result,$"{e}.Current")}}
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Aggregate_seed_func_resultSelector(int 番号, Enumerable source,string seed, FuncFragment func,FuncFragment resultSelector) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => $$"""
        {{FuncSupport(func,nameof(func))}}
        {{FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(func,resultSelector)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct(FuncReturnTypeName(resultSelector))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(func,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            {{e}}.TryGetSpan(out var __span);
            foreach(var __item in __span){
                {{FuncAssign(func,nameof(func),result,result,"__item")}}
            }
            {{FuncAssign(resultSelector,nameof(resultSelector),$"this.{currentValue}",result)}}
            current=this.{{currentValue}};
            return true;
            """
        : $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            while({{e}}.MoveNext()){
                {{FuncAssign(func,nameof(func),result,result,$"{e}.Current")}}
            }
            {{FuncAssign(resultSelector,nameof(resultSelector),$"this.{currentValue}",result)}}
            current=this.{{currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
