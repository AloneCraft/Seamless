namespace Shared.TpcE.Transactions;
/// <summary>Trade-Order(TPC-E 3.3.7)の手順。LINQ は書かない(問い合わせは TradeOrderQueries に書く)。仮の実装。</summary>
internal sealed class TradeOrderFrames{
    public TradeOrderOutput Run(TpcEDatabase db,TradeOrderInput input){
        throw new NotImplementedException("Trade-Order is not implemented yet.");
    }
}
