namespace Shared.TpcE.Transactions;
/// <summary>Trade-Lookup(TPC-E 3.3.6)の手順。LINQ は書かない(問い合わせは TradeLookupQueries に書く)。仮の実装。</summary>
internal sealed class TradeLookupFrames{
    public TradeLookupOutput Run(TpcEDatabase db,TradeLookupInput input){
        throw new NotImplementedException("Trade-Lookup is not implemented yet.");
    }
}
