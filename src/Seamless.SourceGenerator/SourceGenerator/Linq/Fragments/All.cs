namespace Seamless.SourceGenerator.Linq.Fragments;
public class All(int 番号, Enumerable source, FuncFragment predicate) : Scalar(番号) {
    public override string StructMethod => this.DefineStruct(predicate,nameof(predicate));
    public override string GetEnumeratorStruct => this.DefineScalarEnumeratorStruct("bool");
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{source.ElementType}}, bool> {{nameof(predicate)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private bool {{this.currentValue}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{source.ElementType}}, bool> predicate) {
            this.{{nameof(predicate)}} = predicate;
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = true;
            {{this.yielded}} = false;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default;
            return false;
        }
        {{this.yielded}} = true;
        while({{this.e}}.MoveNext()) {
            if(!{{nameof(predicate)}}({{this.e}}.Current)) {
                {{this.currentValue}} = false;
                current = {{this.currentValue}};
                return true;
            }
        }
        {{this.currentValue}} = true;
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
        var {{this.result}}=true;
        """;
    public override string GetBeginLoop => source.GetBeginLoop;
    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        if(!{{this.FuncInvoke(predicate,nameof(predicate),source.GetElement)}}) {
            {{result}}=false;
            goto {{End}};
        }
        """;
    public override string GetEndLoopInside => source.GetEndLoopInside;
    public override string GetEndLoop => source.GetEndLoop;
    public override string GetEndLoopOutside => $$"""
        {{End}}:
        {{source.GetEndLoopOutside}}
        """;
    public override string GetEndGlobal => source.GetEndGlobal;
    public override string Result => this.result;
}
