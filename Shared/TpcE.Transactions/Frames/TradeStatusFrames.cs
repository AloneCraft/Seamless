namespace Shared.TpcE.Transactions;
/// <summary>Trade-Status(TPC-E 3.3.9)の手順。LINQ は書かない(問い合わせは TradeStatusQueries に書く)。</summary>
internal sealed class TradeStatusFrames{
    private readonly TradeStatusQueries queries=new();
    public TradeStatusOutput Run(TpcEDatabase db,TradeStatusInput input){
        // Frame 1(3.3.9.3)。口座の取引を T_DTS の降順に先頭 50 件(索引 X7)。
        var recent=db.AccountTrades.TopDesc(input.AccountId,TpcESpec.MaxTradeStatusLength);
        var trades=this.queries.Recent(db,recent);
        // 口座の持ち主とブローカー(CUSTOMER_ACCOUNT→CUSTOMER、CUSTOMER_ACCOUNT→BROKER)。
        var account=db.CustomerAccount.Get(new CustomerAccountKey(input.AccountId));
        var customer=db.Customer.Get(new CustomerKey(account.CustomerId));
        var broker=db.Broker.Get(new BrokerKey(account.BrokerId));
        // 戻り値の検査(3.3.9.3)。規模が小さいと 50 件に届かず -911 になるのは正常。
        var status=0;
        if(trades.Length!=TpcESpec.MaxTradeStatusLength){
            status=-911;
        }
        return new TradeStatusOutput{
            Status=status,
            NumFound=trades.Length,
            Trades=trades,
            CustomerLastName=customer.LastName,
            CustomerFirstName=customer.FirstName,
            BrokerName=broker.Name,
        };
    }
}
