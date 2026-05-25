namespace Seamless.SourceGenerator.Linq.Fragments;
public class Any(int 番号,Enumerable source):Scalar(番号){
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
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
