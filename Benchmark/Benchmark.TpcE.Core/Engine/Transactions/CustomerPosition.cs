namespace Benchmark.TpcE.Core;
// Customer-Position(TPC-E 3.3.2)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Customer-Position の入力。</summary>
public sealed record CustomerPositionInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.CustomerPosition;
}
/// <summary>Customer-Position の出力。</summary>
public sealed record CustomerPositionOutput:TxnResult;
