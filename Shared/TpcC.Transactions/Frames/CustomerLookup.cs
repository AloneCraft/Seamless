namespace Shared.TpcC.Transactions;
/// <summary>Payment と Order-Status が共通で使う、顧客の決め方(姓なら ceil(n/2) 番目、無ければ番号)。</summary>
internal sealed class CustomerLookup(CustomerQueries queries){
    public int Resolve(TpcCPartition partition,int warehouse,int district,int customerId,string? last){
        if(last is null){
            return customerId;
        }
        var entries=partition.CustomerByName.All(new CustomerNameOwner(warehouse,district,Code16.From(last)));
        if(entries.Length==0){
            // 該当者がいなければ番号のまま進む(HammerDB の手続きと同じ。初期データでは各地区に 1000 種の姓が全部いるので起きない)。
            return customerId;
        }
        return queries.PickByName(entries);
    }
}
