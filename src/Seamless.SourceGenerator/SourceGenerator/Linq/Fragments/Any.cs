namespace Seamless.SourceGenerator.Linq.Fragments;
public class Any(int 番号,Enumerable source):Scalar(番号){
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private bool {{this.currentValue}} = false;
        private bool {{this.yielded}} = false;
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
}
