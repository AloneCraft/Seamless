namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>
    /// Customer-Position の入力を作る(3.3.2.1、6.4.1)。顧客は層の取引頻度で重みづけて選び、50% は納税者番号で、50% は顧客 ID で指す。
    /// 50% で履歴も求め、そのとき履歴を見る口座の添字を顧客の口座数の範囲から一様に選ぶ(選び方は仕様に無いので推測)。
    /// </summary>
    public CustomerPositionInput CustomerPosition(ref Rng rng){
        var customer=this.Model.PickCustomerByTier(ref rng);
        long customerId=0;
        Code24 taxId=default;
        if(rng.Percent(TpcESpec.CustomerPositionByTaxIdPercent)){
            taxId=this.Model.TaxIdOfCustomer(customer);
        }else{
            customerId=customer;
        }
        var getHistory=rng.Percent(TpcESpec.CustomerPositionGetHistoryPercent);
        var accountIdIndex=-1;
        if(getHistory){
            accountIdIndex=rng.Range(0,this.Model.AccountCountOf(customer)-1);
        }
        return new CustomerPositionInput{CustomerId=customerId,TaxId=taxId,GetHistory=getHistory,AccountIdIndex=accountIdIndex};
    }
}
