namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Trade-Order の入力を作る(3.3.7.1、6.4.1 の割合)。取引 ID は運転側が払い出した <see cref="CeContext.NextTradeId"/>。</summary>
    public TradeOrderInput TradeOrder(ref Rng rng){
        var customer=this.Model.PickCustomerByTier(ref rng);
        var account=this.Model.PickAccountOf(customer,ref rng);
        var securities=this.Model.SecuritiesOfAccount(account);
        var security=securities[rng.Range(0,securities.Length-1)];
        // 実行者: 10% は持ち主以外(権限者がいる口座なら権限者、いなければ本人)。
        var person=0;
        var permissions=this.Model.PermissionCountOf(account);
        if(permissions>=2&&rng.Percent(TpcESpec.TradeOrderThirdPartyPercent)){
            person=rng.Range(1,permissions-1);
        }
        var (taxId,lastName,firstName)=this.Model.PermittedPerson(account,person);
        var typeIndex=rng.Weighted(FixedData.TradeTypeWeights);
        var type=FixedData.TradeTypes[typeIndex];
        int[] quantities=[100,200,400,800];
        decimal requested=0m;
        if(!type.IsMarket){
            requested=rng.Range(TpcEModel.PriceLowCents+100,TpcEModel.PriceHighCents-100)/100m;
        }
        var input=new TradeOrderInput{
            AccountId=account,
            ExecFirstName=firstName,
            ExecLastName=lastName,
            ExecTaxId=taxId,
            RequestedPrice=requested,
            TradeQuantity=quantities[rng.Range(0,3)],
            TradeTypeId=type.Id,
            TypeIsMargin=rng.Percent(TpcESpec.TradeOrderMarginPercent),
            IsLifo=rng.Percent(TpcESpec.TradeOrderLifoPercent),
            RollItBack=rng.Range(1,TpcESpec.TradeOrderRollbackOneIn)==1,
            TradeId=this.Context.NextTradeId,
        };
        if(rng.Percent(TpcESpec.TradeOrderByCompanyNamePercent)){
            return input with{CompanyName=this.Model.CompanyName(this.Model.CompanyOfSecurity(security)),Issue=this.Model.IssueOf(security)};
        }
        return input with{Symbol=this.Model.SymbolOf(security)};
    }
}
