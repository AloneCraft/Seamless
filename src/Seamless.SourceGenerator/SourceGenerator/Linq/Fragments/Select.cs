namespace Seamless.SourceGenerator.Linq.Fragments;
public class Select(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号){
    public override string StructMethod => this.DefineStruct(selector,nameof(selector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{source.ElementType}}, {{this.ElementType}}> {{nameof(selector)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{source.ElementType}}, {{this.ElementType}}> selector) {
            {{this.e}} = source.GetEnumerator();
            this.{{nameof(selector)}} = selector;
            {{this.currentValue}} = default!;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.currentValue}} = {{nameof(selector)}}({{this.item}});
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;

    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        {{var_List(this.result,this.ElementType)}}
        """;
    public override string GetBeginLoopOutside => $$"""
        {{source.GetBeginLoopOutside}}
        {{this.result}}.Clear();
        """;
    public override string GetBeginLoop=>source.GetBeginLoop;
    public override string GetBeginLoopInside=>source.GetBeginLoopInside;
    public override string GetEndLoopInside=>source.GetEndLoopInside;
    public override string GetEndLoop=>source.GetEndLoop;
    public override string GetEndLoopOutside =>source.GetEndLoopOutside;
    public override string GetEndGlobal =>source.GetEndGlobal;
    public override string GetAdd => $$"""
        {{this.result}}.Add({{this.GetElement}});
        """;

    public override string GetElement=> $$"""
        {{this.FuncInvoke(selector,nameof(selector),source.GetElement)}}
        """;

    public override string MoveNext => source.MoveNext;
    public override string ElementType => FuncReturnTypeName(selector);
    public override string Result => this.result;
}
