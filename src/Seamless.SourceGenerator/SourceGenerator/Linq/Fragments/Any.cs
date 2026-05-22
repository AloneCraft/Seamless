namespace Seamless.SourceGenerator.Linq.Fragments;
public class Any(int 番号,Enumerable source):Scalar(番号){
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => this.DefineScalarEnumeratorStruct("bool");
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private bool {{this.currentValue}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = false;
            {{this.yielded}} = false;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default;
            return false;
        }
        {{this.yielded}} = true;
        {{this.currentValue}} = {{this.e}}.MoveNext();
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
        var {{this.result}}=false;
        """;
    public override string GetBeginLoop=>$$"""
        {{source.GetBeginLoop}}
        """;
    public override string GetBeginLoopInside=>$$"""
        {{source.GetBeginLoopInside}}
        """;
    public override string GetEndLoopInside=>$$"""
        {{source.GetEndLoopInside}}
        {{result}}=true;
        goto {{End}};
        """;
    public override string GetEndLoop=>$$"""
        {{source.GetEndLoop}}
        """;
    public override string GetEndLoopOutside =>$$"""
        {{End}}:
        {{source.GetEndLoopOutside}}
        """;
    public override string GetEndGlobal =>$$"""
        {{source.GetEndGlobal}}
        """;

    public override string Result => this.result;
}
