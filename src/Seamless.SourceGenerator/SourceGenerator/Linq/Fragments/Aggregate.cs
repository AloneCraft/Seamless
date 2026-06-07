namespace Seamless.SourceGenerator.Linq.Fragments;
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(source.ElementType)}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            {{this.e}}.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            {{source.ElementType}} {{this.result}}=default!;
            var __first=true;
            foreach(var __item in __span){
                if(__first){
                    {{this.result}}=__item;
                    __first=false;
                }else{
                    {{this.FuncAssign(func,nameof(func),this.result,this.result,"__item")}}
                }
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """
        : $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            if(!{{this.e}}.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            var {{this.result}}={{this.e}}.Current;
            while({{this.e}}.MoveNext()){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,$"{this.e}.Current")}}
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Aggregate_seed_func(int 番号, Enumerable source,string seed, FuncFragment func) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(FuncParameterTypeName(func,0))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            {{this.e}}.TryGetSpan(out var __span);
            foreach(var __item in __span){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,"__item")}}
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """
        : $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            while({{this.e}}.MoveNext()){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,$"{this.e}.Current")}}
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Aggregate_seed_func_resultSelector(int 番号, Enumerable source,string seed, FuncFragment func,FuncFragment resultSelector) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => $$"""
        {{this.FuncSupport(func,nameof(func))}}
        {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(func,resultSelector)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(FuncReturnTypeName(resultSelector))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(func,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            {{this.e}}.TryGetSpan(out var __span);
            foreach(var __item in __span){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,"__item")}}
            }
            {{this.FuncAssign(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
            current=this.{{this.currentValue}};
            return true;
            """
        : $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            while({{this.e}}.MoveNext()){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,$"{this.e}.Current")}}
            }
            {{this.FuncAssign(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
            current=this.{{this.currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
