namespace Benchmark.TpcE.Core;
// Trade-Order(TPC-E 3.3.7)の入出力。
/// <summary>Trade-Order の入力(3.3.7.1)。TradeId は運転側が払い出す(巻き戻しでも戻さない)。</summary>
public sealed record TradeOrderInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeOrder;
    public long AccountId{get;init;}
    public string ExecFirstName{get;init;}="";
    public string ExecLastName{get;init;}="";
    public Code24 ExecTaxId{get;init;}
    /// <summary>会社名で銘柄を指定するときの会社名と発行区分。記号指定なら空。</summary>
    public string CompanyName{get;init;}="";
    public Code8 Issue{get;init;}
    /// <summary>記号で指定するときの銘柄記号。会社名指定なら既定値(空)。</summary>
    public Code16 Symbol{get;init;}
    public decimal RequestedPrice{get;init;}
    public int TradeQuantity{get;init;}
    public Code8 TradeTypeId{get;init;}
    public bool TypeIsMargin{get;init;}
    public bool IsLifo{get;init;}
    public bool RollItBack{get;init;}
    public long TradeId{get;init;}
}
/// <summary>Trade-Order の出力。MEE が使う項目(銘柄・種別・数量・要求価格・成行か・巻き戻したか)を含む。</summary>
public sealed record TradeOrderOutput:TxnResult{
    public long TradeId{get;init;}
    public decimal BuyValue{get;init;}
    public decimal SellValue{get;init;}
    public decimal TaxAmount{get;init;}
    public decimal CommissionAmount{get;init;}
    public decimal ChargeAmount{get;init;}
    public decimal MarketPrice{get;init;}
    public decimal RequestedPrice{get;init;}
    public decimal AccountAssets{get;init;}
    public string SecurityName{get;init;}="";
    public Code16 Symbol{get;init;}
    public Code8 TradeTypeId{get;init;}
    public int Quantity{get;init;}
    public bool IsMarket{get;init;}
    public bool IsSell{get;init;}
    public Code8 StatusId{get;init;}
    public bool RolledBack{get;init;}
}
