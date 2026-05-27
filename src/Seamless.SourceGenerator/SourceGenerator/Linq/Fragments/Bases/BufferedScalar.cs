namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class BufferedScalar(int 番号,string resultType) : Scalar(番号) {
    protected abstract string ComputeExpression { get; }
    protected virtual string ComputeMembers => string.Empty;
    protected virtual string StructSources => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{StructSources}}
        {{DefineScalarEnumeratorStruct(resultType)}}
        """;
    public override string GetEnumeratorFields => $$"""
        private readonly {{resultType}} {{result}} = {{ComputeExpression}};
        private bool {{yielded}} = false;
        {{ComputeMembers}}
        """;
    public override string GetTryGetNextBody => $$"""
        if({{yielded}}) {
            current = default!;
            return false;
        }
        {{yielded}} = true;
        current = {{result}};
        return true;
        """;
    public override string GetCurrentExpression => result;
}
