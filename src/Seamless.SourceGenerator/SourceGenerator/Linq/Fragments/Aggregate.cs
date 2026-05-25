namespace Seamless.SourceGenerator.Linq.Fragments;
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => NeedsStructClosure(func) ? string.Empty : this.DefineStruct(func,nameof(func));
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(func)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.ClosureArgsSuffix(func)})"
            : func.CanInlineAssign && func.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(func,nameof(func))})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(source.ElementType)}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(func)
            ? $"TEnum {this.e}, {this.ClosurePrimaryCtorSuffix(func)}"
            : func.CanInlineAssign && func.CanGenerateMethod
                ? $"TEnum {this.e}"
                : $"TEnum {this.e}, global::System.Func<{source.ElementType}, {source.ElementType}, {source.ElementType}> func";
    public override string GetEnumeratorFields => $$"""
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default!;
            return false;
        }
        {{this.yielded}} = true;
        if(!{{this.e}}.MoveNext()) {
            throw global::Seamless.Linq.Enumerable.NoElements();
        }
        var {{this.result}} = {{this.e}}.Current;
        while({{this.e}}.MoveNext()) {
            {{this.result}} = {{this.FuncInvokeField(func,nameof(func),this.result,$"{this.e}.Current")}};
        }
        {{this.currentValue}} = {{this.result}};
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Aggregate_seed_func(int 番号, Enumerable source,string seed, FuncFragment func) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => NeedsStructClosure(func) ? string.Empty : this.DefineStruct(func,nameof(func));
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(func)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.ClosureArgsSuffix(func)})"
            : func.CanInlineAssign && func.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(func,nameof(func))})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(FuncParameterTypeName(func,0))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(func)
            ? $"TEnum {this.e}, {this.ClosurePrimaryCtorSuffix(func)}"
            : func.CanInlineAssign && func.CanGenerateMethod
                ? $"TEnum {this.e}"
                : $"TEnum {this.e}, global::System.Func<{FuncParameterTypeName(func,0)}, {source.ElementType}, {FuncReturnTypeName(func)}> func";
    public override string GetEnumeratorFields => $$"""
        private {{FuncParameterTypeName(func,0)}} {{this.currentValue}} = default!;
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default!;
            return false;
        }
        {{this.yielded}} = true;
        var {{this.result}} = {{seed}};
        while({{this.e}}.MoveNext()) {
            {{this.result}} = {{this.FuncInvokeField(func,nameof(func),this.result,$"{this.e}.Current")}};
        }
        {{this.currentValue}} = {{this.result}};
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Aggregate_seed_func_resultSelector(int 番号, Enumerable source,string seed, FuncFragment func,FuncFragment resultSelector) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => $$"""
        {{(NeedsStructClosure(func) ? string.Empty : this.DefineStruct(func,nameof(func)))}}
        {{(NeedsStructClosure(resultSelector) ? string.Empty : this.DefineStruct(resultSelector,nameof(resultSelector)))}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(func,nameof(func))}, {this.FuncDelegate(resultSelector,nameof(resultSelector))})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(FuncReturnTypeName(resultSelector))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $$"""TEnum {{this.e}}, global::System.Func<{{FuncParameterTypeName(func,0)}}, {{source.ElementType}}, {{FuncReturnTypeName(func)}}> func, global::System.Func<{{FuncParameterTypeName(resultSelector,0)}}, {{FuncReturnTypeName(resultSelector)}}> resultSelector""";
    public override string GetEnumeratorFields => $$"""
        private {{FuncReturnTypeName(resultSelector)}} {{this.currentValue}} = default!;
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default!;
            return false;
        }
        {{this.yielded}} = true;
        var {{this.result}} = {{seed}};
        while({{this.e}}.MoveNext()) {
            {{this.result}} = {{nameof(func)}}({{this.result}}, {{this.e}}.Current);
        }
        {{this.currentValue}} = {{nameof(resultSelector)}}({{this.result}});
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
