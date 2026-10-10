namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Data-Maintenance の入力を作る(仮。取引の実装で置き換える)。tableIndex は 3.3.11 の一覧の順(0〜11)。</summary>
    public DataMaintenanceInput DataMaintenance(ref Rng rng,int tableIndex){
        throw new NotImplementedException("Data-Maintenance input generation is not implemented yet.");
    }
}
