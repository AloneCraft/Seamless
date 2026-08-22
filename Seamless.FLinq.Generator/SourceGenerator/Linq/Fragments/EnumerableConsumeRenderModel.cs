namespace Seamless.FLinq.Generator;

// Compatibility adapter for the former direct-consume model.
// The ordering and fallback behavior are owned by DirectConsumeRenderPlan.
internal sealed class EnumerableConsumeRenderModel(
    int number,
    string enumeratorExpression,
    string elementType,
    string consumerName,
    bool canTryProvideSpan,
    bool canProvideSpan,
    string? directSpanExpression) {
    internal string Render() {
        return new DirectConsumeRenderPlan(
            number,
            enumeratorExpression,
            elementType,
            consumerName,
            canTryProvideSpan,
            canProvideSpan,
            directSpanExpression).Render();
    }
}
