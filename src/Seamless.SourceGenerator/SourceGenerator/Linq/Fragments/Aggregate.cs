namespace Seamless.SourceGenerator.Linq.Fragments;
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    public override string StructMethod => this.DefineStruct(func,nameof(func));
    public override string GetEnumeratorStruct => this.DefineScalarEnumeratorStruct(source.ElementType);
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{source.ElementType}}, {{source.ElementType}}, {{source.ElementType}}> {{nameof(func)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{source.ElementType}} {{this.currentValue}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{source.ElementType}}, {{source.ElementType}}, {{source.ElementType}}> func) {
            this.{{nameof(func)}} = func;
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = default!;
            {{this.yielded}} = false;
        }
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
            {{this.result}} = {{nameof(func)}}({{this.result}}, {{this.e}}.Current);
        }
        {{this.currentValue}} = {{this.result}};
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    public override string GetBeginGlobal => $$"""
                                               {{source.GetBeginGlobal}}
                                               """;

    public override string GetBeginLoopOutside => $$"""
        {{source.GetBeginLoopOutside}}
        {{source.ElementType}} {{result}}=default;
        var {{hasValue}}=false;
        """;

    public override string GetBeginLoop => $$"""
        {{source.GetBeginLoop}}
        """;

    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        if({{hasValue}})goto {{Start}};
        {{result}}={{source.Result}};
        {{hasValue}}=true;
        goto {{Skip}};
        {{Start}}:
        {{FuncAssign(func,nameof(func),result,result,source.Result)}}
        """;

    public override string GetEndLoopInside => $$"""
        {{Skip}}:;
        {{source.GetEndLoopInside}}
        """;

    public override string GetEndLoop => $$"""
        {{source.GetEndLoop}}
        """;

    public override string GetEndLoopOutside => $$"""
        if({{hasValue}})goto {{End}};
        throw NoElements();
        {{End}}:;
        {{source.GetEndLoopOutside}}
        """;

    public override string GetEndGlobal => $$"""
        {{source.GetEndGlobal}}
        """;

    public override string Result => result;
}
public class Aggregate_seed_func(int 番号, Enumerable source,string seed, FuncFragment func) : Scalar(番号) {
    public override string StructMethod => this.DefineStruct(func,nameof(func));
    public override string GetEnumeratorStruct => this.DefineScalarEnumeratorStruct(FuncParameterTypeName(func,0));
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{FuncParameterTypeName(func,0)}}, {{source.ElementType}}, {{FuncReturnTypeName(func)}}> {{nameof(func)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{FuncParameterTypeName(func,0)}} {{this.currentValue}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{FuncParameterTypeName(func,0)}}, {{source.ElementType}}, {{FuncReturnTypeName(func)}}> func) {
            this.{{nameof(func)}} = func;
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = default!;
            {{this.yielded}} = false;
        }
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
        {{this.currentValue}} = {{this.result}};
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        """;
        //             $$"""
        //                 {{source.BeginLoopOutside}}
        //                 var {{result}}={{seed}};
        //                 """,
        //             source.BeginLoop,
        //             $$"""
        //                 {{source.BeginLoopInside}}
        //                 {{result}}={{FuncInvoke(Struct,func,nameof(func),result,source.Element)}};
        //                 """,

    public override string GetBeginLoopOutside => $$"""
        {{source.GetBeginLoopOutside}}
        var {{result}}={{seed}};
        """;

    public override string GetBeginLoop => $$"""
        {{source.GetBeginLoop}}
        """;

    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        {{result}}={{FuncInvoke(func,nameof(func),result,source.GetElement)}};
        """;

    public override string GetEndLoopInside => source.GetEndLoopInside;

    public override string GetEndLoop => $$"""
        {{source.GetEndLoop}}
        """;

    public override string GetEndLoopOutside => source.GetEndLoopOutside;

    public override string GetEndGlobal => $$"""
        {{source.GetEndGlobal}}
        """;

    public override string Result => result;
}
public class Aggregate_seed_func_resultSelector(int 番号, Enumerable source,string seed, FuncFragment func,FuncFragment resultSelector) : Scalar(番号) {
    public override string StructMethod => $$"""
        {{this.DefineStruct(func,nameof(func))}}
        {{this.DefineStruct(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorStruct => this.DefineScalarEnumeratorStruct(FuncReturnTypeName(resultSelector));
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{FuncParameterTypeName(func,0)}}, {{source.ElementType}}, {{FuncReturnTypeName(func)}}> {{nameof(func)}};
        private readonly global::System.Func<{{FuncParameterTypeName(resultSelector,0)}}, {{FuncReturnTypeName(resultSelector)}}> {{nameof(resultSelector)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{FuncReturnTypeName(resultSelector)}} {{this.currentValue}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source,
            global::System.Func<{{FuncParameterTypeName(func,0)}}, {{source.ElementType}}, {{FuncReturnTypeName(func)}}> func,
            global::System.Func<{{FuncParameterTypeName(resultSelector,0)}}, {{FuncReturnTypeName(resultSelector)}}> resultSelector) {
            this.{{nameof(func)}} = func;
            this.{{nameof(resultSelector)}} = resultSelector;
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = default!;
            {{this.yielded}} = false;
        }
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
    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        """;
        //             $$"""
        //                 {{source.BeginLoopOutside}}
        //                 var {{result}}={{seed}};
        //                 """,
        //             source.BeginLoop,
        //             $$"""
        //                 {{source.BeginLoopInside}}
        //                 {{result}}={{FuncInvoke(Struct,func,nameof(func),result,source.Element)}};
        //                 """,

    public override string GetBeginLoopOutside => $$"""
        {{source.GetBeginLoopOutside}}
        var {{result}}={{seed}};
        """;

    public override string GetBeginLoop => $$"""
        {{source.GetBeginLoop}}
        """;

    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        {{result}}={{FuncInvoke(func,nameof(func),result,source.GetElement)}};
        """;

    public override string GetEndLoopInside => source.GetEndLoopInside;

    public override string GetEndLoop => $$"""
        {{source.GetEndLoop}}
        """;

    public override string GetEndLoopOutside => source.GetEndLoopOutside;

    public override string GetEndGlobal => $$"""
        {{source.GetEndGlobal}}
        """;

    public override string Result => this.FuncInvoke(resultSelector,nameof(resultSelector),result);
}
