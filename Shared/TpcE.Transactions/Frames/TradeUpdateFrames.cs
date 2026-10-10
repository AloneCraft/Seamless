namespace Shared.TpcE.Transactions;
/// <summary>Trade-Update(TPC-E 3.3.10)の手順。LINQ は書かない(問い合わせは TradeUpdateQueries に書く)。仮の実装。</summary>
internal sealed class TradeUpdateFrames{
    public TradeUpdateOutput Run(TpcEDatabase db,TradeUpdateInput input){
        throw new NotImplementedException("Trade-Update is not implemented yet.");
    }
}
