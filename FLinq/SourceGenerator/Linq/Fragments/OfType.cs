namespace Seamless.FLinq.Linq.Fragments;

public class OfType(int 番号, Enumerable source, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private bool IsIdentityType => string.Equals(resultElementType,source.ElementType,StringComparison.Ordinal);
    internal Enumerable Source => source;
    internal string ResultElementType => resultElementType;
    internal bool IsIdentity => this.IsIdentityType;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => this.IsIdentityType
        ? source.DirectEnumerableExpression
        : source.DirectEnumerableExpression is { } sourceExpression
        ? $"global::System.Linq.Enumerable.OfType<{resultElementType}>({sourceExpression})"
        : null;
    public override string GetEnumerableExpression => this.IsIdentityType
        ? source.GetEnumerableExpression
        : this.DirectEnumerableExpression ?? base.GetEnumerableExpression;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.MoveNext()) {
            if({{this.e}}.Current is {{resultElementType}} {{this.item}}) {
                current = this.{{this.currentValue}} = {{this.item}};
                return true;
            }
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => resultElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.IsIdentityType) {
            return source.GetConsumeExecutionStatement(consumerName);
        }
        if(this.DirectEnumerableExpression is not { } directEnumerable) {
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
