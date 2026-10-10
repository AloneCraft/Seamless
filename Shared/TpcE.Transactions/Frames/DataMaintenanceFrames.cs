namespace Shared.TpcE.Transactions;
/// <summary>Data-Maintenance(TPC-E 3.3.11)の手順。LINQ は書かない(問い合わせは DataMaintenanceQueries に書く)。仮の実装。</summary>
internal sealed class DataMaintenanceFrames{
    public DataMaintenanceOutput Run(TpcEDatabase db,DataMaintenanceInput input){
        throw new NotImplementedException("Data-Maintenance is not implemented yet.");
    }
}
