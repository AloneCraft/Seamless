namespace Seamless.FLinq.Linq.Fragments;

public class Index(int 番号, Enumerable source) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.ValueEnumerableExtensions.Index({source.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $"private int {this.a}=0;";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            current=this.{{this.currentValue}}=(this.{{this.a}}++,{{this.e}}.Current);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => $"(int Index, {source.ElementType} Item)";
}
