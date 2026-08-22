namespace Seamless.FLinq.Generator;

// direct-consumeで共通する「source enumerator取得→direct span取得」の描画を構造化する。
// RangeRootなどの特殊経路は各Variantの既存ロジックを維持し、このModelはspan経路だけを担当する。
internal sealed class DirectConsumeSourceRenderModel(
    string sourceName,
    string sourceEnumeratorExpression,
    string spanName,
    string directSpanExpression
) {
    internal string RenderSetup() =>
        $"using var {sourceName}={sourceEnumeratorExpression};\n"+
        "global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();\n"+
        $"var {spanName}={directSpanExpression};";
}