namespace Seamless.FLinq.Linq.Fragments;

public class OfType(int 番号, Enumerable source, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private bool IsIdentityType => string.Equals(resultElementType,source.ElementType,StringComparison.Ordinal);
    internal Enumerable Source => source;
    internal string ResultElementType => resultElementType;
    internal bool IsIdentity => IsIdentityType;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => IsIdentityType
        ? source.DirectEnumerableExpression
        : source.DirectEnumerableExpression is { } sourceExpression
        ? $"global::System.Linq.Enumerable.OfType<{resultElementType}>({sourceExpression})"
        : null;
    public override string? NonEnumeratedCountExpression => IsIdentityType ? source.NonEnumeratedCountExpression : null;
    public override bool CountPreserving => IsIdentityType && source.CountPreserving;
    public override bool CanProvideSpan => IsIdentityType && source.CanProvideSpan;
    public override bool CanTryProvideSpan => IsIdentityType && source.CanTryProvideSpan;
    public override string GetEnumerableExpression => IsIdentityType
        ? source.GetEnumerableExpression
        : DirectEnumerableExpression ?? base.GetEnumerableExpression;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => IsIdentityType
        ? $"return this.{e}.TryGetNonEnumeratedCount(out count);"
        : base.GetTryGetNonEnumeratedCountBody;
    protected override string GetTryGetSpanBody => IsIdentityType
        ? $"return this.{e}.TryGetSpan(out span);"
        : base.GetTryGetSpanBody;
    protected override string GetTryCopyToBody => IsIdentityType
        ? $"return this.{e}.TryCopyTo(destination,__offset);"
        : base.GetTryCopyToBody;
    public override string GetTryGetNextBody => $$"""
        while({{e}}.MoveNext()) {
            if({{e}}.Current is {{resultElementType}} {{item}}) {
                current = this.{{currentValue}} = {{item}};
                return true;
            }
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => resultElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(IsIdentityType) {
            return source.GetConsumeExecutionStatement(consumerName);
        }
        if(DirectEnumerableExpression is not { } directEnumerable) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var valueName=$"__cv{番号}";
        return $$"""
            foreach(var {{valueName}} in {{directEnumerable}}) {
                {{consumerName}}.Consume({{valueName}});
            }
            """;
    }
}
