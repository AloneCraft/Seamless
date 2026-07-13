namespace Seamless.FLinq.Linq.Fragments;

public sealed class PersistedSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface:false){
    public override string ProviderName=>"PersistedSet";
    public override string SourceVariablePrefix=>"__persistedIterateSource";
    public override string StrategyInterfaceTypeName=>"global::Seamless.Persisted.Collections.ISetIterateStrategy";
}
