namespace Shared.TpcE.Transactions;
/// <summary>Trade-Result(TPC-E 3.3.8)の手順。LINQ は書かない(問い合わせは TradeResultQueries に書く)。仮の実装。</summary>
internal sealed class TradeResultFrames{
    public TradeResultOutput Run(TpcEDatabase db,TradeResultInput input){
        throw new NotImplementedException("Trade-Result is not implemented yet.");
    }
}
