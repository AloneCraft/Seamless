namespace Seamless.FLinq;

// Renders the default direct-consume path shared by Enumerable fragments.
// Operator-specific fast paths remain in their owning fragments.
internal sealed class EnumerableConsumeRenderModel(
    int number,
    string enumeratorExpression,
    string elementType,
    string consumerName,
    bool canTryProvideSpan,
    bool canProvideSpan,
    string? directSpanExpression) {
    internal string Render() {
        var builder = new StringBuilder();
        var enumeratorName = $"__ce{number}";
        var valueName = $"__cv{number}";
        builder.AppendLine($"using var {enumeratorName}={enumeratorExpression};");
        TraversalSupport.AppendTraversalLoop(
            builder,
            enumeratorName,
            elementType,
            valueName,
            $"{consumerName}.Consume({valueName});",
            canTryProvideSpan,
            canProvideSpan,
            directSpanExpression);
        return builder.ToString().TrimEnd();
    }
}