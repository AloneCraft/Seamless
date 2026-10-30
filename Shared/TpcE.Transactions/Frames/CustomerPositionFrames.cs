namespace Shared.TpcE.Transactions;
/// <summary>Customer-Position(TPC-E 3.3.2)の手順。LINQ は書かない(問い合わせは CustomerPositionQueries に書く)。</summary>
internal sealed class CustomerPositionFrames{
    /// <summary>Frame 2 で履歴を集める直近の取引の数(疑似コードの first 10 rows)。</summary>
    private const int HistoryTradeCount=10;
    /// <summary>Frame 2 の履歴の件数の下限(hist_len < 10 なら -221)。</summary>
    private const int MinHistoryLength=10;
    private readonly CustomerPositionQueries queries=new();
    public CustomerPositionOutput Run(TpcEDatabase db,CustomerPositionInput input){
        var output=this.Frame1(db,input.CustomerId,input.TaxId);
        if(output.Status!=0||!input.GetHistory){
            // Frame 3(3.3.2.5)。DB は読まずに確定するだけ。
            return output;
        }
        // Frame 2 に渡す口座は、Frame 1 の口座一覧の acct_id_idx 番目。
        if(input.AccountIdIndex<0||input.AccountIdIndex>=output.Accounts.Length){
            throw new ArgumentOutOfRangeException(nameof(input),input.AccountIdIndex,$"Customer-Position: acct_id_idx is outside the {output.Accounts.Length} accounts.");
        }
        var history=this.Frame2(db,output.Accounts[input.AccountIdIndex].AccountId);
        return output with{Status=history.Status,HistoryLength=history.History.Length,History=history.History};
    }
    /// <summary>Frame 1(3.3.2.3)。cust_id が 0 なら納税者番号で顧客を引き、口座ごとの資産を求める。口座が無いなどの誤りは Status -211。</summary>
    public CustomerPositionOutput Frame1(TpcEDatabase db,long requestedCustomerId,Code24 taxId){
        var customerId=requestedCustomerId;
        if(customerId==0){
            var found=db.CustomerByTaxId.All(taxId);
            if(found.Length!=1){
                throw new KeyNotFoundException($"Customer-Position: tax id '{taxId}' matched {found.Length} customers.");
            }
            customerId=found[0];
        }
        var customer=db.Customer.Get(new CustomerKey(customerId));
        // 口座(索引 X2)と、口座ごとの保有の時価の材料を集めて、口座ごとの資産を求める。
        var accountIds=db.CustomerAccounts.All(customerId);
        var accounts=new CustomerAccountRow[accountIds.Length];
        var holdings=new List<HoldingValue>();
        for(var index=0;index<accountIds.Length;index++){
            accounts[index]=db.CustomerAccount.Get(new CustomerAccountKey(accountIds[index]));
            holdings.AddRange(this.queries.HoldingValues(db,accountIds[index]));
        }
        var positions=this.queries.Positions(db,accounts,holdings.ToArray());
        var output=new CustomerPositionOutput{
            CustomerId=customerId,
            CustomerStatusId=customer.StatusId,
            LastName=customer.LastName,
            FirstName=customer.FirstName,
            MiddleInitial=customer.MiddleInitial,
            Gender=customer.Gender,
            Tier=customer.Tier,
            DateOfBirth=customer.DateOfBirth,
            AddressId=customer.AddressId,
            Country1=customer.Country1,
            Area1=customer.Area1,
            Local1=customer.Local1,
            Extension1=customer.Extension1,
            Country2=customer.Country2,
            Area2=customer.Area2,
            Local2=customer.Local2,
            Extension2=customer.Extension2,
            Country3=customer.Country3,
            Area3=customer.Area3,
            Local3=customer.Local3,
            Extension3=customer.Extension3,
            Email1=customer.Email1,
            Email2=customer.Email2,
            AccountLength=positions.Length,
            Accounts=positions,
        };
        // 戻り値の検査(3.3.2.3)。口座が無いと Frame 2 の acct_id[acct_id_idx] が引けないので、ここで終える(仕様は打ち切りを決めていない)。
        if(positions.Length<1||positions.Length>TpcESpec.MaxAccountLength){
            return output with{Status=-211};
        }
        return output;
    }
    /// <summary>Frame 2(3.3.2.4)。口座の直近 10 取引(索引 X7、T_DTS の降順)の状態の記録を集める。件数が範囲外なら Status -221。</summary>
    public (int Status,CustomerPositionHistory[] History) Frame2(TpcEDatabase db,long accountId){
        var recent=db.AccountTrades.TopDesc(accountId,CustomerPositionFrames.HistoryTradeCount);
        var statusTypes=this.queries.StatusTypes(db);
        var trades=new TradeRow[recent.Length];
        var histories=new List<TradeHistoryRow>();
        for(var index=0;index<recent.Length;index++){
            trades[index]=db.Trade.Get(new TradeKey(recent[index].Id));
            // TRADE_HISTORY は主キー (t_id, st_id) を状態の数だけ当てる。
            foreach(var statusType in statusTypes){
                if(db.TradeHistory.TryGet(new TradeHistoryKey(recent[index].Id,statusType.Id),out var history)){
                    histories.Add(history);
                }
            }
        }
        var events=this.queries.History(db,trades,histories.ToArray(),statusTypes);
        // 戻り値の検査(3.3.2.4)。
        var status=0;
        if(events.Length<CustomerPositionFrames.MinHistoryLength||events.Length>TpcESpec.MaxHistoryLength){
            status=-221;
        }
        return (status,events);
    }
}
