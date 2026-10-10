namespace Benchmark.TpcE.Core;
// Broker-Volume(TPC-E 3.3.1)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Broker-Volume の入力。</summary>
public sealed record BrokerVolumeInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.BrokerVolume;
}
/// <summary>Broker-Volume の出力。</summary>
public sealed record BrokerVolumeOutput:TxnResult;
