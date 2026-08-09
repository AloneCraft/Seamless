namespace Seamless.FLinq;

internal sealed class UnorderedOperation(
    int number,
    string methodName,
    string sourceTypeName):Scalar(number), DirectExecutionFragment {
    protected override Enumerable PrimarySource => null!;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => string.Empty;
    public override string GetTryGetNextBody => string.Empty;
    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) =>
        $"{actualTypeName} {actualVariableName}=global::Seamless.Linq.GeneratedCodeHelpers.ThrowUnorderedSourceOperation<{actualTypeName}>(\"{methodName}\",\"{sourceTypeName}\");";
}
