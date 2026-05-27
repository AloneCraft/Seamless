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
        private {{source.ElementType}} {{currentValue}} = default!;
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{yielded}}) {
            current = default!;
            return false;
        }
        {{yielded}} = true;
        if(!{{e}}.MoveNext()) {
            throw global::Seamless.Linq.Enumerable.NoElements();
        }
        var {{result}} = {{e}}.Current;
        while({{e}}.MoveNext()) {
            {{result}} = {{FuncInvokeField(func,nameof(func),result,$"{e}.Current")}};
        }
        {{currentValue}} = {{result}};
        current = {{currentValue}};
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
        private {{FuncParameterTypeName(func,0)}} {{currentValue}} = default!;
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{yielded}}) {
            current = default!;
            return false;
        }
        {{yielded}} = true;
        var {{result}} = {{seed}};
        while({{e}}.MoveNext()) {
            {{result}} = {{FuncInvokeField(func,nameof(func),result,$"{e}.Current")}};
        }
        {{currentValue}} = {{result}};
        current = {{currentValue}};
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
        private {{FuncReturnTypeName(resultSelector)}} {{currentValue}} = default!;
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{yielded}}) {
            current = default!;
            return false;
        }
        {{yielded}} = true;
        var {{result}} = {{seed}};
        while({{e}}.MoveNext()) {
            {{result}} = {{FuncInvokeField(func,nameof(func),result,$"{e}.Current")}};
        }
        {{currentValue}} = {{FuncInvokeField(resultSelector,nameof(resultSelector),result)}};
        current = {{currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
