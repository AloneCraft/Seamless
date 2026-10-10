namespace Benchmark.TpcE.Core;
// Data-Maintenance(TPC-E 3.3.11)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Data-Maintenance の入力。</summary>
public sealed record DataMaintenanceInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.DataMaintenance;
}
/// <summary>Data-Maintenance の出力。</summary>
public sealed record DataMaintenanceOutput:TxnResult;
