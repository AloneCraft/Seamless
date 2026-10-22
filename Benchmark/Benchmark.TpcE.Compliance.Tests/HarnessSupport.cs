using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>ハーネスの試験が使う、既存の実装で DB に状態を作る補助。</summary>
internal static class HarnessSupport{
    /// <summary>指値の買い注文(TLB)を <paramref name="count"/> 件、異なる銘柄で出す。未約定の注文(TRADE_REQUEST)ができる。</summary>
    public static void PlaceLimitOrders(ITpcEEngine engine,int count){
        var db=engine.Database;
        var accounts=db.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).Take(count).ToArray();
        var securities=db.Security.EnumerateRows().OrderBy(static row=>row.Symbol).Take(count).ToArray();
        for(var index=0;index<count&&index<accounts.Length&&index<securities.Length;index++){
            var customer=db.Customer.Get(new CustomerKey(accounts[index].CustomerId));
            var company=db.Company.Get(new CompanyKey(securities[index].CompanyId));
            var result=(TradeOrderOutput)engine.Execute(new TradeOrderInput{
                AccountId=accounts[index].Id,
                ExecFirstName=customer.FirstName,
                ExecLastName=customer.LastName,
                ExecTaxId=customer.TaxId,
                CompanyName=company.Name,
                Issue=securities[index].Issue,
                Symbol=securities[index].Symbol,
                RequestedPrice=100.00m,
                TradeQuantity=100,
                TradeTypeId=Code8.From("TLB"),
                IsLifo=true,
                TradeId=900_000_000_000_000+index,
                Now=TpcETime.FromDateTime(new DateTime(2026,1,1))+index,
            });
            if(result.Status<0){
                throw new InvalidOperationException($"The setup Trade-Order failed with status {result.Status}.");
            }
        }
    }
}
