namespace Seamless.FLinq;

/// <summary>
/// MethodBodyRewriter が生成する、順序が意味を持つ文のまとまりです。
/// </summary>
internal sealed class MethodBodyRenderPlan {
    private readonly GeneratedCodeBlock body;

    private MethodBodyRenderPlan(GeneratedCodeBlock body) {
        this.body=body;
    }

    internal static MethodBodyRenderPlan Empty { get; } = new(GeneratedCodeBlock.Empty);

    internal static MethodBodyRenderPlan Statement(string statement) {
        return new MethodBodyRenderPlan(GeneratedCodeBlock.Leaf(statement));
    }

    internal static MethodBodyRenderPlan Sequence(params MethodBodyRenderPlan[] plans) {
        return new MethodBodyRenderPlan(GeneratedCodeBlock.Sequence(
            plans.Select(static plan => plan.body).ToArray()));
    }

    internal static MethodBodyRenderPlan ScalarRead(
        string resultTypeName,
        string resultVariableName,
        string enumeratorName,
        string enumeratorExpression) {
        return Statement(new ScalarExecutionRenderModel(
            resultTypeName,
            resultVariableName,
            enumeratorName,
            enumeratorExpression).RenderRead());
    }

    internal static MethodBodyRenderPlan ConsumerAssignment(string consumerName,string expression) {
        return Statement($"var {consumerName}={expression};");
    }

    internal static MethodBodyRenderPlan Consume(string consumerName,string valueName) {
        return Statement($"{consumerName}.Consume({valueName});");
    }

    internal static MethodBodyRenderPlan MaterializeLoop(string valueName,string consumerName) {
        return Statement($"foreach(var __item in {valueName}) {{\n    {consumerName}.Consume(__item);\n}}");
    }

    internal string Render() => this.body.Render();

    internal void AppendTo(StringBuilder builder) {
        var rendered=this.Render();
        if(rendered.Length>0) {
            builder.AppendLine(rendered);
        }
    }
}
