namespace Seamless.SourceGenerator.Linq.Fragments;

public abstract class BufferedSequence(int 番号,string elementType) : Enumerable(番号) {
    protected abstract string BuildExpression { get; }
    protected virtual string BuildMembers => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.StructSources}}
        {{base.GetEnumeratorStruct}}
        """;
    protected virtual string StructSources => string.Empty;
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.List<{{this.ElementType}}>.Enumerator {{this.e}} = {{this.BuildExpression}}.GetEnumerator();
        private {{this.ElementType}} {{this.currentValue}} = default!;
        {{this.BuildMembers}}
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            {{this.currentValue}} = {{this.e}}.Current;
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => elementType;
}
