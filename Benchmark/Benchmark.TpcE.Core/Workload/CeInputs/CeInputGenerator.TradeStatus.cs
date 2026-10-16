namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Trade-Status の入力を作る(3.3.9.1)。顧客を層の取引頻度で重みづけて選び、その口座から一様に 1 つ。</summary>
    public TradeStatusInput TradeStatus(ref Rng rng){
        var customer=this.Model.PickCustomerByTier(ref rng);
        return new TradeStatusInput{AccountId=this.Model.PickAccountOf(customer,ref rng)};
    }
}
