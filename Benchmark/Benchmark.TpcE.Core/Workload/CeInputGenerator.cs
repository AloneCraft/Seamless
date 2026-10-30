namespace Benchmark.TpcE.Core;
/// <summary>
/// CE(顧客と仲介業者の模擬)と DM の取引の入力を作る。DB は読まず、模型(<see cref="TpcEModel"/>)と文脈(<see cref="CeContext"/>)だけを使う。
/// 取引ごとの作り方は CeInputs/CeInputGenerator.*.cs に書く。
/// </summary>
public sealed partial class CeInputGenerator(CeContext context){
    public CeContext Context{get;}=context;
    private TpcEModel Model=>this.Context.Model;
    /// <summary>CE が出す 8 種の取引の比率(6.3.1)。並びは <see cref="CeKinds"/> と同じ。</summary>
    private static readonly int[] Mix=[TpcESpec.MixBrokerVolume,TpcESpec.MixCustomerPosition,TpcESpec.MixMarketWatch,TpcESpec.MixSecurityDetail,TpcESpec.MixTradeLookup,TpcESpec.MixTradeOrder,TpcESpec.MixTradeStatus,TpcESpec.MixTradeUpdate];
    public static readonly TxnKind[] CeKinds=[TxnKind.BrokerVolume,TxnKind.CustomerPosition,TxnKind.MarketWatch,TxnKind.SecurityDetail,TxnKind.TradeLookup,TxnKind.TradeOrder,TxnKind.TradeStatus,TxnKind.TradeUpdate];
    /// <summary>比率どおりに CE の取引の種類を選ぶ。</summary>
    public static TxnKind PickKind(ref Rng rng)=>CeKinds[rng.Weighted(Mix)];
    /// <summary>指定の種類の入力を作る。</summary>
    public TxnRequest Generate(TxnKind kind,ref Rng rng){
        TxnRequest request=kind switch{
            TxnKind.BrokerVolume=>this.BrokerVolume(ref rng),
            TxnKind.CustomerPosition=>this.CustomerPosition(ref rng),
            TxnKind.MarketWatch=>this.MarketWatch(ref rng),
            TxnKind.SecurityDetail=>this.SecurityDetail(ref rng),
            TxnKind.TradeLookup=>this.TradeLookup(ref rng),
            TxnKind.TradeOrder=>this.TradeOrder(ref rng),
            TxnKind.TradeStatus=>this.TradeStatus(ref rng),
            TxnKind.TradeUpdate=>this.TradeUpdate(ref rng),
            _=>throw new ArgumentOutOfRangeException(nameof(kind),kind,"Not a CE transaction."),
        };
        return request with{Now=this.Context.Now};
    }
}
