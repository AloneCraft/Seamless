namespace Seamless.FLinq;

// Enumerator型とconstructor引数の組み立てを構造化し、各Fragmentの補間式を共通化する。
// 引数の順序と文字列の区切りは既存のGetEnumeratorExpressionと同じ。
internal sealed class EnumeratorExpressionRenderModel(
    string enumeratorTypeName,
    IReadOnlyList<string> arguments,string separator=", ") {
    internal string Render() =>
        $"new {enumeratorTypeName}({string.Join(separator,arguments)})";
}