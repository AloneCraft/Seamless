namespace Seamless.FLinq.Generator;

// Scalar FragmentをEnumerator経路で実行するときの結果変数・using・TryGetNextを構造化する。
// 実行順序とDispose順序は既存MethodBodyRewriterの3行構成をそのまま保持する。
internal sealed class ScalarExecutionRenderModel(
    string resultTypeName,
    string resultVariableName,
    string enumeratorName,
    string enumeratorExpression
) {
    internal string RenderRead() =>
        $"{resultTypeName} {resultVariableName}=default!;\n"+
        $"using var {enumeratorName}={enumeratorExpression};\n"+
        $"{enumeratorName}.TryGetNext(out {resultVariableName});";
}