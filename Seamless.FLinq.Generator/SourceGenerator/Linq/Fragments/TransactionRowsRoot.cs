namespace Seamless.FLinq.Generator;

internal sealed class TransactionRowsRoot(int number,string sourceExpression,string elementType,string enumeratorTypeName)
    :ProviderNativeRootBase(number,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface:true){
    public override string ProviderName=>"TransactionRows";
    public override string SourceVariablePrefix=>"__transactionRowsSource";
    public override string StrategyInterfaceTypeName=>"global::Seamless.Collections.Iteration.IStrategy";
    public override string? NativeCountExpression=>null;
    public override SourceCapability Capabilities=>base.Capabilities&~SourceCapability.Unordered;
}
