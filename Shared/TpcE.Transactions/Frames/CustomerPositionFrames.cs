namespace Shared.TpcE.Transactions;
/// <summary>Customer-Position(TPC-E 3.3.2)の手順。LINQ は書かない(問い合わせは CustomerPositionQueries に書く)。仮の実装。</summary>
internal sealed class CustomerPositionFrames{
    public CustomerPositionOutput Run(TpcEDatabase db,CustomerPositionInput input){
        throw new NotImplementedException("Customer-Position is not implemented yet.");
    }
}
