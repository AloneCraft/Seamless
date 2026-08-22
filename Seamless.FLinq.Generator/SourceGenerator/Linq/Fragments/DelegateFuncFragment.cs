namespace Seamless.FLinq.Generator;

internal sealed class DelegateFuncFragment(
    string delegateTypeName,
    string delegateFieldName,
    string delegateConstructorArg)
    :FuncFragment {

    public override bool CanInvokeAsDelegate => true;
    public override string? DelegateTypeName => delegateTypeName;
    public override string? DelegateFieldName => delegateFieldName;
    public override string? DelegateConstructorArg => delegateConstructorArg;
}
