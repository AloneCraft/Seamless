namespace Seamless.SourceGenerator.Linq.Fragments;
public class All(int 番号, Enumerable source, FuncFragment predicate) : Scalar(番号) {
    public override string StructMethod => this.DefineStruct(predicate,nameof(predicate));
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private bool {{this.currentValue}} = true;
        private bool {{this.yielded}} = false;
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
}
