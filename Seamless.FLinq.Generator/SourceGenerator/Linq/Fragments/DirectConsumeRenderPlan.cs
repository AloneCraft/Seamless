namespace Seamless.FLinq;

// direct-consume経路の順序を明示するRender契約です。
// span、TryGetSpan、TryGetNextの選択は既存TraversalSupportの判定を維持します。
internal sealed class DirectConsumeRenderPlan {
    private readonly int number;
    private readonly string enumeratorExpression;
    private readonly string elementType;
    private readonly string consumerName;
    private readonly bool canTryProvideSpan;
    private readonly bool canProvideSpan;
    private readonly string? directSpanExpression;

    internal DirectConsumeRenderPlan(
        int number,
        string enumeratorExpression,
        string elementType,
        string consumerName,
        bool canTryProvideSpan,
        bool canProvideSpan,
        string? directSpanExpression) {
        this.number=number;
        this.enumeratorExpression=enumeratorExpression;
        this.elementType=elementType;
        this.consumerName=consumerName;
        this.canTryProvideSpan=canTryProvideSpan;
        this.canProvideSpan=canProvideSpan;
        this.directSpanExpression=directSpanExpression;
    }

    internal string Render() {
        var builder=new StringBuilder();
        var enumeratorName=$"__ce{this.number}";
        var valueName=$"__cv{this.number}";
        builder.AppendLine($"using var {enumeratorName}={this.enumeratorExpression};");
        new EnumeratorTraversalRenderModel(
            enumeratorName,
            this.elementType,
            valueName,
            $"{this.consumerName}.Consume({valueName});",
            this.canTryProvideSpan,
            this.canProvideSpan,
            this.directSpanExpression).AppendTo(builder);
        return builder.ToString().TrimEnd();
    }
}
