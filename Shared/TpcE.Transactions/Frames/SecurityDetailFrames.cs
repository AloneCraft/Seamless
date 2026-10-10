namespace Shared.TpcE.Transactions;
/// <summary>Security-Detail(TPC-E 3.3.5)の手順。LINQ は書かない(問い合わせは SecurityDetailQueries に書く)。仮の実装。</summary>
internal sealed class SecurityDetailFrames{
    public SecurityDetailOutput Run(TpcEDatabase db,SecurityDetailInput input){
        throw new NotImplementedException("Security-Detail is not implemented yet.");
    }
}
