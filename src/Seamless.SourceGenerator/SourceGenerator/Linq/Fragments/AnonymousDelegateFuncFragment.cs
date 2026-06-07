namespace Seamless.SourceGenerator.Linq.Fragments;

// 匿名型を返すラムダを Enumerator のデリゲートフィールドとして保持するための FuncFragment。
// 匿名型の型名はコード上に書けないため CanInlineAssign=false、デリゲート経由で呼び出す。
internal sealed class AnonymousDelegateFuncFragment : LambdaFunc {
    // CanInlineAssign はフィールド（デフォルト false）のため override 不要
    public override bool CanInvokeAsDelegate => true;
    public override bool RequiresResultTypeParameter => true;
    public override string? DelegateTypeName { get; }
    public override string? DelegateFieldName { get; }
    public override string? DelegateConstructorArg { get; }

    public AnonymousDelegateFuncFragment(
        string delegateTypeName,
        string delegateFieldName,
        string delegateConstructorArg) {
        this.DelegateTypeName = delegateTypeName;
        this.DelegateFieldName = delegateFieldName;
        this.DelegateConstructorArg = delegateConstructorArg;
    }
}
