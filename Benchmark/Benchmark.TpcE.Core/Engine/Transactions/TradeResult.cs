namespace Benchmark.TpcE.Core;
// Trade-Result(TPC-E 3.3.8)の入出力。
/// <summary>Trade-Result の入力。TradePrice は MEE が決めた約定価格。</summary>
public sealed record TradeResultInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeResult;
    public long TradeId{get;init;}
    public decimal TradePrice{get;init;}
}
/// <summary>Trade-Result の出力。</summary>
public sealed record TradeResultOutput:TxnResult{
    public long TradeId{get;init;}
    public decimal AccountBalance{get;init;}
    public decimal BuyValue{get;init;}
    public decimal SellValue{get;init;}
    public decimal TaxAmount{get;init;}
    public decimal CommissionAmount{get;init;}
    public decimal SettlementAmount{get;init;}
    public int HoldingsTouched{get;init;}
}
