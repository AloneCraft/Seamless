namespace Benchmark.TpcE.Core;
/// <summary>
/// 固定表(TRADE_TYPE、STATUS_TYPE、CHARGE、COMMISSION_RATE、TAXRATE)の中身と、それを引く関数。
/// 生成器と、初期取引の約定(Trade-Result と同じ式)で同じ値を使う。仕様書に中身が載っているのは TRADE_TYPE と STATUS_TYPE だけ(2.2.5.9、2.2.7.2)で、
/// 残りは件数(2.6.1.9)と制約に合う合成値。
/// </summary>
public static class FixedData{
    public static readonly Code8 Active=Code8.From("ACTV");
    public static readonly Code8 Completed=Code8.From("CMPT");
    public static readonly Code8 Canceled=Code8.From("CNCL");
    public static readonly Code8 Pending=Code8.From("PNDG");
    public static readonly Code8 Submitted=Code8.From("SBMT");
    public static readonly StatusTypeRow[] StatusTypes=[
        new(Active,"Active"),
        new(Completed,"Completed"),
        new(Canceled,"Canceled"),
        new(Pending,"Pending"),
        new(Submitted,"Submitted"),
    ];
    public static readonly Code8 MarketBuy=Code8.From("TMB");
    public static readonly Code8 MarketSell=Code8.From("TMS");
    public static readonly Code8 LimitBuy=Code8.From("TLB");
    public static readonly Code8 LimitSell=Code8.From("TLS");
    public static readonly Code8 StopLoss=Code8.From("TSL");
    /// <summary>並びは取引種別の割合(1.4.4.2)と同じ: TMB 30%、TMS 30%、TLB 20%、TLS 10%、TSL 10%。</summary>
    public static readonly TradeTypeRow[] TradeTypes=[
        new(MarketBuy,"Market-Buy",false,true),
        new(MarketSell,"Market-Sell",true,true),
        new(LimitBuy,"Limit-Buy",false,false),
        new(LimitSell,"Limit-Sell",true,false),
        new(StopLoss,"Stop-Loss",true,false),
    ];
    public static readonly int[] TradeTypeWeights=[30,30,20,10,10];
    public static int TradeTypeIndex(Code8 id){
        for(var index=0;index<TradeTypes.Length;index++){
            if(TradeTypes[index].Id==id){
                return index;
            }
        }
        throw new ArgumentException($"Unknown trade type {id}.",nameof(id));
    }
    /// <summary>料金(層 1 が最も高い、2.2.4.2)。</summary>
    public static decimal Charge(int typeIndex,int tier){
        decimal[] byTier=[10.00m,7.50m,5.00m];
        decimal[] byType=[0.00m,0.00m,1.50m,1.50m,2.00m];
        return byTier[tier-1]+byType[typeIndex];
    }
    /// <summary>手数料率の数量帯(両端を含む)。取引数量 100/200/400/800 がどれかに入る。</summary>
    public static readonly (long From,long To)[] CommissionBands=[(0,199),(200,399),(400,799),(800,999_999)];
    /// <summary>手数料率(百分率、0 より大きい)。層・数量帯・取引所で下がる。</summary>
    public static decimal CommissionRate(int tier,int typeIndex,int exchangeIndex,int bandIndex){
        decimal[] byTier=[0.50m,0.40m,0.30m];
        return byTier[tier-1]-0.05m*bandIndex+0.01m*exchangeIndex+0.005m*typeIndex;
    }
    public static int CommissionBand(int quantity){
        for(var index=0;index<CommissionBands.Length;index++){
            if(CommissionBands[index].From<=quantity&&quantity<=CommissionBands[index].To){
                return index;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(quantity));
    }
    public static int ExchangeIndex(Code8 id){
        for(var index=0;index<TpcEModel.Exchanges.Length;index++){
            if(TpcEModel.Exchanges[index]==id){
                return index;
            }
        }
        throw new ArgumentException($"Unknown exchange {id}.",nameof(id));
    }
    /// <summary>税率(0〜1)。国の税は高め、地域の税は低め。名前には Data-Maintenance が切り替える " Tax " を含める。</summary>
    public static TaxrateRow Taxrate(int index){
        if(index<TpcEModel.NationalTaxrates.Length){
            var id=TpcEModel.NationalTaxrates[index];
            decimal[] rates=[0.10m,0.15m,0.25m,0.28m,0.33m,0.15m,0.22m,0.26m,0.29m];
            string country;
            if(id.StartsWith("US",StringComparison.Ordinal)){
                country="US Federal";
            }else{
                country="Canada Federal";
            }
            return new(Code8.From(id),country+" Income Tax Bracket "+id[2],rates[index]);
        }
        var division=index-TpcEModel.NationalTaxrates.Length;
        var divisionId=TpcEModel.DivisionTaxrateId(division);
        return new(divisionId,"Division "+divisionId+" Sales Tax Rate",(division%40)*0.0025m);
    }
}
