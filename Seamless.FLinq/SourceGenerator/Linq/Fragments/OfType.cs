namespace Seamless.FLinq.Linq.Fragments;

internal class OfType(int 番号,Enumerable source,string resultElementType):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private bool IsIdentityType => string.Equals(resultElementType,source.ElementType,StringComparison.Ordinal);
    internal Enumerable Source => source;
    internal string ResultElementType => resultElementType;
    internal bool IsIdentity => this.IsIdentityType;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => this.IsIdentityType ? source.DirectEnumerableExpression : null;
    public override string? NonEnumeratedCountExpression => this.IsIdentityType ? source.NonEnumeratedCountExpression : null;
    public override bool CountPreserving => this.IsIdentityType&&source.CountPreserving;
    public override bool CanProvideSpan => this.IsIdentityType&&source.CanProvideSpan;
    public override bool CanTryProvideSpan => this.IsIdentityType&&source.CanTryProvideSpan;
    public override string GetEnumerableExpression => this.IsIdentityType
        ? source.GetEnumerableExpression
        : this.DirectEnumerableExpression??base.GetEnumerableExpression;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => this.IsIdentityType
        ? $"return this.{this.e}.TryGetNonEnumeratedCount(out count);"
        : base.GetTryGetNonEnumeratedCountBody;
    protected override string GetTryGetSpanBody => this.IsIdentityType
        ? $"return this.{this.e}.TryGetSpan(out span);"
        : base.GetTryGetSpanBody;
    protected override string GetTryCopyToBody => this.IsIdentityType
        ? $"return this.{this.e}.TryCopyTo(destination,__offset);"
        : base.GetTryCopyToBody;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.TryGetNext(out var __mn45)) {
            if(__mn45 is {{resultElementType}} {{this.item}}) {
                current = this.{{this.currentValue}} = {{this.item}};
                return true;
            }
        }
        current = default!;
        return false;
        """;
    public override string ElementType => resultElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.IsIdentityType) {
            return source.GetConsumeExecutionStatement(consumerName);
        }
        if(this.DirectEnumerableExpression is not { } directEnumerable) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var valueName = $"__cv{番号}";
        return $$"""
            foreach(var {{valueName}} in {{directEnumerable}}) {
                {{consumerName}}.Consume({{valueName}});
            }
            """;
    }
}
