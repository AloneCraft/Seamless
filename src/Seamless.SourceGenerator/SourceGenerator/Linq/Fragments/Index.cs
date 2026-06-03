namespace Seamless.SourceGenerator.Linq.Fragments;

public class Index(int 番号, Enumerable source) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $"private int {a}=0;";
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            current=this.{{currentValue}}=(this.{{a}}++,{{e}}.Current);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"(int Index, {source.ElementType} Item)";
}
