namespace Shared.TpcE.Transactions;
/// <summary>Trade-Result(TPC-E 3.3.8)の手順。LINQ は書かない(税率・手数料率は TradeOrderQueries を使う)。</summary>
internal sealed class TradeResultFrames{
    private readonly TradeOrderQueries queries=new();
    public TradeResultOutput Run(TpcEDatabase db,TradeResultInput input){
        var now=input.Now;
        // Frame 1: 取引・取引種別・保有の要約。
        var found=db.Trade.Find(new TradeKey(input.TradeId));
        if(found is not TradeRow trade){
            return new TradeResultOutput{Status=-811,TradeId=input.TradeId};
        }
        var tradeType=db.TradeType.Get(new TradeTypeKey(trade.TypeId));
        var account=db.CustomerAccount.Get(new CustomerAccountKey(trade.AccountId));
        var customer=db.Customer.Get(new CustomerKey(account.CustomerId));
        var price=input.TradePrice;
        // Frame 2: 保有の更新。
        var buyValue=0m;
        var sellValue=0m;
        var needed=trade.Quantity;
        long summaryQuantity=0;
        var summaryKey=new HoldingSummaryKey(trade.AccountId,trade.Symbol);
        var summary=db.HoldingSummary.Find(summaryKey);
        if(summary is HoldingSummaryRow summaryRow){
            summaryQuantity=summaryRow.Quantity;
        }
        var touched=0;
        var sign=1;
        if(tradeType.IsSell){
            sign=-1;
        }
        // 保有の要約を、取り崩す前に挿入・更新する。ちょうど 0 になる場合は最後に削除する。
        if(summaryQuantity==0){
            db.HoldingSummary.Insert(new HoldingSummaryRow(trade.AccountId,trade.Symbol,sign*trade.Quantity));
        }else if(summaryQuantity+sign*trade.Quantity!=0){
            db.HoldingSummary.Update(new HoldingSummaryRow(trade.AccountId,trade.Symbol,checked((int)(summaryQuantity+sign*trade.Quantity))));
        }
        var closes=(tradeType.IsSell&&summaryQuantity>0)||(!tradeType.IsSell&&summaryQuantity<0);
        if(closes){
            foreach(var hold in HoldingCursor.Read(db,trade.AccountId,trade.Symbol,trade.Lifo)){
                if(needed==0){
                    break;
                }
                touched++;
                if(tradeType.IsSell){
                    if(hold.Quantity>needed){
                        db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,trade.Id,hold.Quantity,hold.Quantity-needed));
                        db.Holding.Update(hold with{Quantity=hold.Quantity-needed});
                        buyValue+=needed*hold.Price;
                        sellValue+=needed*price;
                        needed=0;
                    }else{
                        db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,trade.Id,hold.Quantity,0));
                        db.Holding.Delete(new HoldingKey(hold.TradeId));
                        buyValue+=hold.Quantity*hold.Price;
                        sellValue+=hold.Quantity*price;
                        needed-=hold.Quantity;
                    }
                }else if(hold.Quantity+needed<0){
                    db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,trade.Id,hold.Quantity,hold.Quantity+needed));
                    db.Holding.Update(hold with{Quantity=hold.Quantity+needed});
                    sellValue+=needed*hold.Price;
                    buyValue+=needed*price;
                    needed=0;
                }else{
                    db.HoldingHistory.Insert(new HoldingHistoryRow(hold.TradeId,trade.Id,hold.Quantity,0));
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
            db.HoldingHistory.Insert(new HoldingHistoryRow(trade.Id,trade.Id,0,sign*needed));
            db.Holding.Insert(new HoldingRow(trade.Id,trade.AccountId,trade.Symbol,now,price,sign*needed));
            touched++;
        }else if(summaryQuantity+sign*trade.Quantity==0&&summaryQuantity!=0){
            db.HoldingSummary.Delete(summaryKey);
        }
        // Frame 3: 税(税区分 1・2 で利益があるとき)。
        var status=0;
        var taxAmount=0m;
        if((account.TaxStatus==1||account.TaxStatus==2)&&sellValue>buyValue){
            var rates=this.queries.TaxRateSum(db,db.CustomerTaxrates.All(account.CustomerId));
            taxAmount=decimal.Round((sellValue-buyValue)*rates,2,MidpointRounding.AwayFromZero);
            db.Trade.Update(db.Trade.Get(new TradeKey(trade.Id)) with{Tax=taxAmount});
            if(taxAmount<=0m){
                status=-831;
            }
        }
        // Frame 4: 手数料率。
        var security=db.Security.Get(new SecurityKey(trade.Symbol));
        var commissionRate=this.queries.CommissionRate(db,customer.Tier,trade.TypeId,security.ExchangeId,trade.Quantity);
        if(commissionRate<=0m&&status==0){
            status=-841;
        }
        var commission=decimal.Round(commissionRate/100m*(trade.Quantity*price),2,MidpointRounding.AwayFromZero);
        // Frame 5: 約定の記録。
        db.Trade.Update(db.Trade.Get(new TradeKey(trade.Id)) with{Commission=commission,Dts=now,StatusId=FixedData.Completed,TradePrice=price});
        db.TradeHistory.Insert(new TradeHistoryRow(trade.Id,now,FixedData.Completed));
        var broker=db.Broker.Get(new BrokerKey(account.BrokerId));
        db.Broker.Update(broker with{CommissionTotal=broker.CommissionTotal+commission,NumTrades=broker.NumTrades+1});
        // Frame 6: 決済。
        var settlement=trade.Quantity*price-trade.Charge-commission;
        if(!tradeType.IsSell){
            settlement=-(trade.Quantity*price+trade.Charge+commission);
        }
        if(account.TaxStatus==1){
            settlement-=taxAmount;
        }
        var cashType="Margin";
        if(trade.IsCash){
            cashType="Cash Account";
        }
        db.Settlement.Insert(new SettlementRow(trade.Id,cashType,TpcETime.DateOf(now)+2,settlement));
        if(trade.IsCash){
            db.CustomerAccount.Update(account with{Balance=account.Balance+settlement});
            db.CashTransaction.Insert(new CashTransactionRow(trade.Id,now,settlement,tradeType.Name+" "+trade.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)+" shares of "+security.Name));
        }
        var balance=db.CustomerAccount.Get(new CustomerAccountKey(trade.AccountId)).Balance;
        return new TradeResultOutput{
            Status=status,
            TradeId=trade.Id,
            AccountBalance=balance,
            BuyValue=buyValue,
            SellValue=sellValue,
            TaxAmount=taxAmount,
            CommissionAmount=commission,
            SettlementAmount=settlement,
            HoldingsTouched=touched,
        };
    }
}
