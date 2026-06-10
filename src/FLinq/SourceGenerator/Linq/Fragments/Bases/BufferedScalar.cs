namespace Tsavorite.SourceGenerator.Linq.Fragments;
public abstract class BufferedScalar(int 番号,string resultType) : Scalar(番号) {
    protected abstract string ComputeExpression { get; }
    protected virtual string ComputeMembers => string.Empty;
    protected virtual string StructSources => string.Empty;
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    public override string GetCurrentExpression => "Current";
    public override string GetEnumeratorStruct => $$"""
        {{this.StructSources}}
        {{this.DefineScalarEnumeratorStruct(resultType)}}
        """;
    public override string GetEnumeratorFields => $$"""
        public {{resultType}} Current { get; private set; }
        private bool {{this.yielded}} = false;
        {{this.ComputeMembers}}
        """;
    public override string GetEnumeratorConstructor => $"this.Current = {this.ComputeExpression};";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}) {
            current = default!;
            return false;
        }
        this.{{this.yielded}} = true;
        current = this.Current;
        return true;
        """;
}
