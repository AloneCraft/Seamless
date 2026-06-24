namespace Seamless.FLinq.Linq.Fragments;

public class Cast(int 番号, Enumerable source, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{e}.TryGetNonEnumeratedCount(out count);";
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            current = this.{{currentValue}} = ({{resultElementType}}){{e}}.Current!;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => resultElementType;
}
