namespace Seamless.FLinq.Linq.Fragments;

public class Cast(int 番号, Enumerable source, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private bool IsIdentityType => string.Equals(resultElementType,source.ElementType,StringComparison.Ordinal);
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override bool CanProvideSpan => IsIdentityType && source.CanProvideSpan;
    public override bool CanTryProvideSpan => IsIdentityType && source.CanTryProvideSpan;
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{e}.TryGetNonEnumeratedCount(out count);";
    protected override string GetTryGetSpanBody => IsIdentityType
        ? $"return this.{e}.TryGetSpan(out span);"
        : base.GetTryGetSpanBody;
    protected override string GetTryCopyToBody => IsIdentityType
        ? $"return this.{e}.TryCopyTo(destination,__offset);"
        : base.GetTryCopyToBody;
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
