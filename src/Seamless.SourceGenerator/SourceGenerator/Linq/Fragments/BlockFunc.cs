using System.Text.RegularExpressions;

namespace Seamless.SourceGenerator.Linq.Fragments;

// 複数文ブロック本体（=>{ ... } / delegate(){ ... }）を「文」としてインライン展開する FuncFragment。
// 単一 return / 式本体は従来どおり LambdaFunc（式インライン）で扱い、それ以外のブロックだけここに来る。
//
// BlockBody には、生成時に return を
//   __SEAMLESS_DEST__ = (expr); goto __SEAMLESS_LABEL__;
// へ書き換え済みのブロック内文テキストが入る（書き換えは Interceptor 側の Roslyn rewriter が行う）。
// RenderInlineAssign 時に __SEAMLESS_DEST__ を代入先へ、__SEAMLESS_LABEL__ を一意ラベルへ、
// ラムダ引数名を実引数へ置換し、末尾にラベルを置いて「文の塊」を作る。
public sealed class BlockFunc : FuncFragment {
    // 引数・dest・label 置換前のブロック内文テキスト。
    public string BlockBody = string.Empty;

    public override string RenderInlineAssign(string destination,int number,params string[] arguments) {
        var label = $"__blockEnd_{number}";
        var prelude = new StringBuilder();
        var body = BlockBody;
        for(var index = 0; index<arguments.Length && index<ParameterNames.Length; index++) {
            if(string.IsNullOrWhiteSpace(ParameterNames[index])) {
                continue;
            }

            var arg = arguments[index];
            string replacement;
            if(IsSimpleArgument(arg)) {
                // 単純な識別子・メンバーアクセスは副作用なし → 直接展開する。
                replacement = arg;
            } else {
                // 副作用のある式はブロックで複数回参照され得るので一時変数へ退避する。
                var tempName = $"__arg{number}_{SanitizeLocalName(destination)}_{index}";
                prelude.AppendLine($"var {tempName}={arg};");
                replacement = tempName;
            }

            body = Regex.Replace(
                body,
                $@"\b{Regex.Escape(ParameterNames[index])}\b",
                replacement,
                RegexOptions.CultureInvariant);
        }

        body = body
            .Replace("__SEAMLESS_DEST__", destination)
            .Replace("__SEAMLESS_LABEL__", label);

        // ブロックを {} で囲ってローカル宣言をスコープに閉じ込め、末尾に goto 先ラベルを置く。
        prelude.Append($"{{\n{body}\n}}\n{label}: ;");
        return prelude.ToString();
    }

    public override string RenderInlineExpression(params string[] arguments) {
        // ブロック本体は文として展開するしかないので、式位置では使わせない（InlinedScalarFunc と同方針）。
        throw new InvalidOperationException("Block-bodied lambda must be rendered as statements.");
    }

    // 識別子・ドット区切りメンバーアクセスだけからなる式は副作用がなく一時変数不要。
    private static bool IsSimpleArgument(string expr) =>
        Regex.IsMatch(expr, @"^[@\w\.]+$");
}
