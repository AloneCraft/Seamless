namespace Seamless.SourceGenerator.Linq.Fragments;

public abstract class BufferedSequence(int 番号,string elementType) : Enumerable(番号) {
    protected abstract string BuildExpression { get; }
    protected virtual string BuildMembers => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{StructSources}}
        {{base.GetEnumeratorStruct}}
        """;
    protected virtual string StructSources => string.Empty;
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.List<{{ElementType}}>.Enumerator {{e}} = {{BuildExpression}}.GetEnumerator();
        {{BuildMembers}}
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            this.Current = {{e}}.Current;
            current = this.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => elementType;
}
