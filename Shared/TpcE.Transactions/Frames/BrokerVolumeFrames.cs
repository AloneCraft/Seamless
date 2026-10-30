namespace Shared.TpcE.Transactions;
/// <summary>Broker-Volume(TPC-E 3.3.1)の手順。LINQ は書かない(問い合わせは BrokerVolumeQueries に書く)。</summary>
internal sealed class BrokerVolumeFrames{
    private readonly BrokerVolumeQueries queries=new();
    public BrokerVolumeOutput Run(TpcEDatabase db,BrokerVolumeInput input){
        // Frame 1(3.3.1.3)。部門の銘柄を SECTOR→INDUSTRY→COMPANY(索引 X14)→SECURITY(索引 X15)でたどる。
        var symbols=new List<Code16>();
        foreach(var sectorId in this.queries.SectorIds(db,input.SectorName)){
            foreach(var industryId in this.queries.IndustryIds(db,sectorId)){
                foreach(var companyId in db.IndustryCompanies.All(industryId)){
                    symbols.AddRange(db.CompanySecurities.All(companyId));
                }
            }
        }
        var brokers=this.queries.Brokers(db,input.BrokerList);
        var entries=this.queries.Volumes(db,symbols.ToArray(),brokers);
        // 戻り値の検査(3.3.1.3)。
        var status=0;
        if(entries.Length<0||entries.Length>TpcESpec.MaxBrokerListLength){
            status=-111;
        }
        return new BrokerVolumeOutput{Status=status,ListLength=entries.Length,Entries=entries};
    }
}
