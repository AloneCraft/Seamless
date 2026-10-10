namespace Shared.TpcE.Transactions;
/// <summary>Trade-Status(TPC-E 3.3.9)の手順。LINQ は書かない(問い合わせは TradeStatusQueries に書く)。仮の実装。</summary>
internal sealed class TradeStatusFrames{
    public TradeStatusOutput Run(TpcEDatabase db,TradeStatusInput input){
        throw new NotImplementedException("Trade-Status is not implemented yet.");
    }
}
