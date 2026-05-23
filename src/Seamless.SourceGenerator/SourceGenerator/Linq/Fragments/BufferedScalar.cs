namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class BufferedScalar(int 番号,string resultType) : Scalar(番号) {
    protected abstract string ComputeExpression { get; }
    protected virtual string ComputeMembers => string.Empty;
    protected virtual string StructSources => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.StructSources}}
        {{this.DefineScalarEnumeratorStruct(resultType)}}
        """;
    public override string GetEnumeratorFields => $$"""
        private readonly {{resultType}} {{this.result}} = {{this.ComputeExpression}};
        private bool {{this.yielded}} = false;
        {{this.ComputeMembers}}
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default!;
            return false;
        }
        {{this.yielded}} = true;
        current = {{this.result}};
        return true;
        """;
    public override string GetCurrentExpression => this.result;
}
