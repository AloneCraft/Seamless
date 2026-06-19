namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class LambdaFunc : FuncFragment {
    public override bool IsIdentity =>
        this.ParameterNames.Length==1
        && !this.RequiresStatementInlining
        && string.Equals(this.InlineBody.Trim(),this.ParameterNames[0],StringComparison.Ordinal);

    // ラムダ本体を代入文として埋め込む。
    public override string RenderInlineAssign(string destination,int number,params string[] arguments) {
        return this.RenderInlineAssignCore(destination,number,arguments);
    }

    // ラムダ本体を式位置へ直接埋め込む。
    public override string RenderInlineExpression(params string[] arguments) {
        // Where の条件式や Select の Add 引数のように、
        // 「値を返すだけの static ラムダ」は式としてその場へ埋め込みたい。
        // これにより展開後コードが helper メソッド中心にならず、
        // 元のラムダの計算内容をそのまま読み取れる。
        return this.RenderInlineExpressionCore(arguments);
    }
}
