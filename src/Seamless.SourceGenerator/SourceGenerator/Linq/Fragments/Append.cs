namespace Seamless.SourceGenerator.Linq.Fragments;

public class Append(int 番号, Enumerable source, string element) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {element})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {source.ElementType} element";
    public override string GetEnumeratorFields => $$"""
        private byte {{state}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{state}}==0) {
            if({{e}}.MoveNext()) {
                current=this.{{currentValue}}={{e}}.Current;
                return true;
            }
            this.{{state}}=1;
        }
        if(this.{{state}}==1) {
            this.{{state}}=2;
            current=this.{{currentValue}}=element;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}
