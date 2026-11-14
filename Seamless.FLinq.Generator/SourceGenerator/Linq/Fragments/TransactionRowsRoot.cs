namespace Seamless.FLinq.Generator;

internal class TransactionRowsRoot(int number,string sourceExpression,string elementType,string enumeratorTypeName,string? keyType=null)
    :ProviderNativeRootBase(number,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface:true){
    public override string ProviderName=>"TransactionRows";
    public override string SourceVariablePrefix=>"__transactionRowsSource";
    public override string StrategyInterfaceTypeName=>"global::Seamless.Collections.Iteration.IStrategy";
    public override string? NativeCountExpression=>null;
    public override SourceCapability Capabilities=>base.Capabilities&~SourceCapability.Unordered;
    internal string? KeyType=>keyType;
    internal (ISymbol Member,string Method)? DeclaredJoinKey { get; set; }
    internal virtual TransactionRowsRoot WithSource(int number,string expression,string elementType,string enumeratorTypeName)=>new(number,expression,elementType,enumeratorTypeName,keyType);
}
