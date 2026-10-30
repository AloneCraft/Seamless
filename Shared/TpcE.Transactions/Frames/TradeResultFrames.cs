namespace Shared.TpcE.Transactions;
/// <summary>Frame 1 が引く、取引とその周辺(3.3.8.3)。</summary>
internal readonly record struct TradeResultTrade(TradeRow Trade,TradeTypeRow Type,CustomerAccountRow Account,CustomerRow Customer,long SummaryQuantity);
/// <summary>
/// Trade-Result(TPC-E 3.3.8)の手順。LINQ は書かない(税率・手数料率は TradeOrderQueries を使う)。
/// 仕様のフレーム 1〜6 を別の関数にし、<see cref="Run"/> はそれらを順に呼ぶ(EGenTxnHarness は同じフレームを自分の順序で呼ぶ)。
/// </summary>
internal sealed class TradeResultFrames{
    private readonly TradeOrderQueries queries=new();
    public TradeResultOutput Run(TpcEDatabase db,TradeResultInput input){
        var now=input.Now;
        // Frame 1: 取引・取引種別・保有の要約。
        if(!this.Frame1(db,input.TradeId,out var info)){
            return new TradeResultOutput{Status=-811,TradeId=input.TradeId};
        }
        var trade=info.Trade;
        var price=input.TradePrice;
        // Frame 2: 保有の更新。
        var holdings=this.Frame2(db,trade.Id,trade.AccountId,info.SummaryQuantity,trade.Lifo,trade.Symbol,trade.Quantity,info.Type.IsSell,price,now);
        // Frame 3: 税(税区分 1・2 で利益があるとき)。
        var status=0;
        var taxAmount=0m;
        if((info.Account.TaxStatus==1||info.Account.TaxStatus==2)&&holdings.SellValue>holdings.BuyValue){
            taxAmount=this.Frame3(db,info.Account.CustomerId,holdings.BuyValue,holdings.SellValue,trade.Id);
            if(taxAmount<=0m){
                status=-831;
            }
        }
        // Frame 4: 手数料率。
        var rate=this.Frame4(db,info.Account.CustomerId,trade.Quantity,trade.Symbol,trade.TypeId,out var securityName);
        if(rate<=0m&&status==0){
            status=-841;
        }
        var commission=decimal.Round(rate/100m*(trade.Quantity*price),2,MidpointRounding.AwayFromZero);
        // Frame 5: 約定の記録。
        this.Frame5(db,info.Account.BrokerId,commission,FixedData.Completed,now,trade.Id,price);
        // Frame 6: 決済。
        var settlement=trade.Quantity*price-trade.Charge-commission;
        if(!info.Type.IsSell){
            settlement=-(trade.Quantity*price+trade.Charge+commission);
        }
        if(info.Account.TaxStatus==1){
            settlement-=taxAmount;
        }
        var balance=this.Frame6(db,trade.AccountId,TpcETime.DateOf(now)+2,securityName,settlement,now,trade.Id,trade.IsCash,trade.Quantity,info.Type.Name);
        return new TradeResultOutput{
            Status=status,
            TradeId=trade.Id,
            AccountBalance=balance,
            BuyValue=holdings.BuyValue,
            SellValue=holdings.SellValue,
            TaxAmount=taxAmount,
            CommissionAmount=commission,
            SettlementAmount=settlement,
            HoldingsTouched=holdings.Touched,
        };
    }
    /// <summary>Frame 1(3.3.8.3)。取引・取引種別・口座・顧客と、その銘柄の保有の数量。取引が無ければ false(num_found=0)。</summary>
    public bool Frame1(TpcEDatabase db,long tradeId,out TradeResultTrade info){
        var found=db.Trade.Find(new TradeKey(tradeId));
        if(found is not TradeRow trade){
            info=default;
            return false;
        }
        var tradeType=db.TradeType.Get(new TradeTypeKey(trade.TypeId));
        var account=db.CustomerAccount.Get(new CustomerAccountKey(trade.AccountId));
        var customer=db.Customer.Get(new CustomerKey(account.CustomerId));
        long summaryQuantity=0;
        var summary=db.HoldingSummary.Find(new HoldingSummaryKey(trade.AccountId,trade.Symbol));
        if(summary is HoldingSummaryRow summaryRow){
            summaryQuantity=summaryRow.Quantity;
        }
        info=new TradeResultTrade(trade,tradeType,account,customer,summaryQuantity);
        return true;
    }
    /// <summary>Frame 2(3.3.8.4)。保有の要約と保有を更新し、取り崩した分の取得額と売却額を返す。touched は触れた保有の数。</summary>
    public (decimal BuyValue,decimal SellValue,int Touched) Frame2(TpcEDatabase db,long tradeId,long accountId,long summaryQuantity,bool isLifo,Code16 symbol,int quantity,bool isSell,decimal price,long now){
        var buyValue=0m;
        var sellValue=0m;
        var needed=quantity;
        var touched=0;
        var sign=1;
        if(isSell){
            sign=-1;
        }
        var summaryKey=new HoldingSummaryKey(accountId,symbol);
        // 保有の要約を、取り崩す前に挿入・更新する。ちょうど 0 になる場合は最後に削除する。
        if(summaryQuantity==0){
            db.HoldingSummary.Insert(new HoldingSummaryRow(accountId,symbol,sign*quantity));
        }else if(summaryQuantity+sign*quantity!=0){
            db.HoldingSummary.Update(new HoldingSummaryRow(accountId,symbol,checked((int)(summaryQuantity+sign*quantity))));
        }
        var closes=(isSell&&summaryQuantity>0)||(!isSell&&summaryQuantity<0);
        if(closes){
            foreach(var hold in HoldingCursor.Read(db,accountId,symbol,isLifo)){
                if(needed==0){
                    break;
                }
                touched++;
                if(isSell){
                    if(hold.Quantity>needed){
                        db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,tradeId,hold.Quantity,hold.Quantity-needed));
                        db.Holding.Update(hold with{Quantity=hold.Quantity-needed});
                        buyValue+=needed*hold.Price;
                        sellValue+=needed*price;
                        needed=0;
                    }else{
                        db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,tradeId,hold.Quantity,0));
                        db.Holding.Delete(new HoldingKey(hold.TradeId));
                        buyValue+=hold.Quantity*hold.Price;
                        sellValue+=hold.Quantity*price;
                        needed-=hold.Quantity;
                    }
                }else if(hold.Quantity+needed<0){
                    db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,tradeId,hold.Quantity,hold.Quantity+needed));
                    db.Holding.Update(hold with{Quantity=hold.Quantity+needed});
                    sellValue+=needed*hold.Price;
                    buyValue+=needed*price;
                    needed=0;
                }else{
                    db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,tradeId,hold.Quantity,0));
                    db.Holding.Delete(new HoldingKey(hold.TradeId));
                    var covered=-hold.Quantity;
                    sellValue+=covered*hold.Price;
                    buyValue+=covered*price;
                    needed-=covered;
                }
            }
        }
        if(needed>0){
            // 残りが新しい保有(売りなら売り持ち、買いなら買い持ち)になる。
            db.HoldingHistory.Insert(new HoldingHistoryRow(tradeId,tradeId,0,sign*needed));
            db.Holding.Insert(new HoldingRow(tradeId,accountId,symbol,now,price,sign*needed));
            touched++;
        }else if(summaryQuantity+sign*quantity==0&&summaryQuantity!=0){
            db.HoldingSummary.Delete(summaryKey);
        }
        return (buyValue,sellValue,touched);
    }
    /// <summary>Frame 3(3.3.8.5)。税額(顧客の税率の合計 × 利益)を求め、TRADE.T_TAX に書く。</summary>
    public decimal Frame3(TpcEDatabase db,long customerId,decimal buyValue,decimal sellValue,long tradeId){
        var rates=this.queries.TaxRateSum(db,db.CustomerTaxrates.All(customerId));
        var taxAmount=decimal.Round((sellValue-buyValue)*rates,2,MidpointRounding.AwayFromZero);
        db.Trade.Update(db.Trade.Get(new TradeKey(tradeId)) with{Tax=taxAmount});
        return taxAmount;
    }
    /// <summary>Frame 4(3.3.8.6)。顧客の階層・取引種別・銘柄の取引所と数量から手数料率を引く。銘柄の名前も返す。</summary>
    public decimal Frame4(TpcEDatabase db,long customerId,int quantity,Code16 symbol,Code8 typeId,out string securityName){
        var customer=db.Customer.Get(new CustomerKey(customerId));
        var security=db.Security.Get(new SecurityKey(symbol));
        securityName=security.Name;
        return this.queries.CommissionRate(db,customer.Tier,typeId,security.ExchangeId,quantity);
    }
    /// <summary>Frame 5(3.3.8.7)。約定の記録: TRADE の手数料・時刻・状態・価格、TRADE_HISTORY、ブローカーの手数料合計と取引数。</summary>
    public void Frame5(TpcEDatabase db,long brokerId,decimal commission,Code8 completed,long tradeDts,long tradeId,decimal price){
        db.Trade.Update(db.Trade.Get(new TradeKey(tradeId)) with{Commission=commission,Dts=tradeDts,StatusId=completed,TradePrice=price});
        db.TradeHistory.Insert(new TradeHistoryRow(tradeId,tradeDts,completed));
        var broker=db.Broker.Get(new BrokerKey(brokerId));
        db.Broker.Update(broker with{CommissionTotal=broker.CommissionTotal+commission,NumTrades=broker.NumTrades+1});
    }
    /// <summary>Frame 6(3.3.8.8)。決済: SETTLEMENT、現金口座なら口座の残高と CASH_TRANSACTION。確定後の口座の残高を返す。</summary>
    public decimal Frame6(TpcEDatabase db,long accountId,long dueDay,string securityName,decimal settlement,long tradeDts,long tradeId,bool isCash,int quantity,string typeName){
        var cashType="Margin";
        if(isCash){
            cashType="Cash Account";
        }
        db.Settlement.Insert(new SettlementRow(tradeId,cashType,dueDay,settlement));
        if(isCash){
            var account=db.CustomerAccount.Get(new CustomerAccountKey(accountId));
            db.CustomerAccount.Update(account with{Balance=account.Balance+settlement});
            db.CashTransaction.Insert(new CashTransactionRow(tradeId,tradeDts,settlement,typeName+" "+quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)+" shares of "+securityName));
        }
        return db.CustomerAccount.Get(new CustomerAccountKey(accountId)).Balance;
    }
}
