namespace Benchmark.TpcE.Core;
// Security-Detail(TPC-E 3.3.5)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Security-Detail の入力。</summary>
public sealed record SecurityDetailInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.SecurityDetail;
}
/// <summary>Security-Detail の出力。</summary>
public sealed record SecurityDetailOutput:TxnResult;
