namespace Seamless.FLinq.Linq.Fragments;

public sealed class TsavoriteSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface:true){
    public override string ProviderName=>"TsavoriteSet";
    public override string SourceVariablePrefix=>"__tsavoriteSetIterateSource";
    public override string StrategyInterfaceTypeName=>"global::Seamless.Tsavorite.Collections.ISetIterateStrategy";
}
