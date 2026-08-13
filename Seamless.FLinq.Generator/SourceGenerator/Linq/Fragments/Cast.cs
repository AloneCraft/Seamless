namespace Seamless.FLinq;

internal class Cast(int 番号,Enumerable source,string resultElementType):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    protected override Enumerable PrimarySource => source;
    private bool IsIdentityType => string.Equals(resultElementType,source.ElementType,StringComparison.Ordinal);
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override bool CanProvideSpan => this.IsIdentityType&&source.CanProvideSpan;
    public override bool CanTryProvideSpan => this.IsIdentityType&&source.CanTryProvideSpan;
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{this.e}.TryGetNonEnumeratedCount(out count);";
    protected override string GetTryGetSpanBody => this.IsIdentityType
        ? $"return this.{this.e}.TryGetSpan(out span);"
        : base.GetTryGetSpanBody;
    protected override string GetTryCopyToBody => this.IsIdentityType
        ? $"return this.{this.e}.TryCopyTo(destination,__offset);"
        : base.GetTryCopyToBody;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var __mn13)){
            current = this.{{this.currentValue}} = ({{resultElementType}})__mn13!;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => resultElementType;
    public override string GetConsumePreparationStatement() =>
        this.IsIdentityType
            ? source.GetConsumePreparationStatement()
            : base.GetConsumePreparationStatement();
    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.IsIdentityType
            ? source.GetConsumeExecutionStatement(consumerName)
            : base.GetConsumeExecutionStatement(consumerName);
}
