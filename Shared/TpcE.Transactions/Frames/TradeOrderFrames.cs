namespace Shared.TpcE.Transactions;
/// <summary>Trade-Order(TPC-E 3.3.7)の手順。LINQ は書かない(問い合わせは TradeOrderQueries に書く)。</summary>
internal sealed class TradeOrderFrames{
    private readonly TradeOrderQueries queries=new();
    public TradeOrderOutput Run(TpcEDatabase db,TradeOrderInput input){
        // Frame 1: 口座・顧客。
        var account=db.CustomerAccount.Find(new CustomerAccountKey(input.AccountId));
        if(account is not CustomerAccountRow accountRow){
            return new TradeOrderOutput{Status=-711,TradeId=input.TradeId};
        }
        var customer=db.Customer.Get(new CustomerKey(accountRow.CustomerId));
        // Frame 2: 実行者が持ち主でなければ権限を確かめる。
        if(input.ExecLastName!=customer.LastName||input.ExecFirstName!=customer.FirstName||input.ExecTaxId!=customer.TaxId){
            var permission=db.AccountPermission.Find(new AccountPermissionKey(input.AccountId,input.ExecTaxId));
            var allowed=false;
            if(permission is AccountPermissionRow permissionRow){
                allowed=permissionRow.FirstName==input.ExecFirstName&&permissionRow.LastName==input.ExecLastName;
            }
            if(!allowed){
                return new TradeOrderOutput{Status=-721,TradeId=input.TradeId};
            }
        }
        // Frame 3: 銘柄の特定と見積もり。
        var symbol=input.Symbol;
        if(input.CompanyName.Length>0){
            symbol=this.FindSymbolByCompany(db,input.CompanyName,input.Issue);
        }
        var security=db.Security.Get(new SecurityKey(symbol));
        var market=db.LastTrade.Get(new LastTradeKey(symbol)).Price;
        var tradeType=db.TradeType.Get(new TradeTypeKey(input.TradeTypeId));
        var requestedPrice=input.RequestedPrice;
        if(tradeType.IsMarket){
            requestedPrice=market;
        }
        // 既存の保有を LIFO/FIFO で取り崩した場合の取得額と売却額(見積もり。書かない)。
        var buyValue=0m;
        var sellValue=0m;
        var needed=input.TradeQuantity;
        long summaryQuantity=0;
        var summary=db.HoldingSummary.Find(new HoldingSummaryKey(input.AccountId,symbol));
        if(summary is HoldingSummaryRow summaryRow){
            summaryQuantity=summaryRow.Quantity;
        }
        if((tradeType.IsSell&&summaryQuantity>0)||(!tradeType.IsSell&&summaryQuantity<0)){
            foreach(var hold in HoldingCursor.Read(db,input.AccountId,symbol,input.IsLifo)){
                if(needed==0){
                    break;
                }
                if(tradeType.IsSell){
                    if(hold.Quantity>needed){
                        buyValue+=needed*hold.Price;
                        sellValue+=needed*requestedPrice;
                        needed=0;
                    }else{
                        buyValue+=hold.Quantity*hold.Price;
                        sellValue+=hold.Quantity*requestedPrice;
                        needed-=hold.Quantity;
                    }
                }else if(hold.Quantity+needed<0){
                    sellValue+=needed*hold.Price;
                    buyValue+=needed*requestedPrice;
                    needed=0;
                }else{
                    var covered=-hold.Quantity;
                    sellValue+=covered*hold.Price;
                    buyValue+=covered*requestedPrice;
                    needed-=covered;
                }
            }
        }
        var taxAmount=0m;
        var taxed=accountRow.TaxStatus==1||accountRow.TaxStatus==2;
        if(sellValue>buyValue&&taxed){
            var rates=this.queries.TaxRateSum(db,db.CustomerTaxrates.All(accountRow.CustomerId));
            taxAmount=decimal.Round((sellValue-buyValue)*rates,2,MidpointRounding.AwayFromZero);
        }
        var commissionRate=this.queries.CommissionRate(db,customer.Tier,input.TradeTypeId,security.ExchangeId,input.TradeQuantity);
        var charge=db.Charge.Get(new ChargeKey(input.TradeTypeId,customer.Tier)).Charge;
        var assets=0m;
        if(input.TypeIsMargin){
            assets=accountRow.Balance+this.queries.HoldingAssets(db,input.AccountId,db.AccountHoldingSummaries.All(input.AccountId));
        }
        var statusId=FixedData.Pending;
        if(tradeType.IsMarket){
            statusId=FixedData.Submitted;
        }
        var status=0;
        if(sellValue>buyValue&&taxed&&taxAmount<=0m){
            status=-731;
        }else if(commissionRate<=0m){
            status=-732;
        }else if(charge==0m){
            status=-733;
        }
        var commission=decimal.Round(commissionRate/100m*input.TradeQuantity*requestedPrice,2,MidpointRounding.AwayFromZero);
        var output=new TradeOrderOutput{
            Status=status,
            TradeId=input.TradeId,
            BuyValue=buyValue,
            SellValue=sellValue,
            TaxAmount=taxAmount,
            CommissionAmount=commission,
            ChargeAmount=charge,
            MarketPrice=market,
            RequestedPrice=requestedPrice,
            AccountAssets=assets,
            SecurityName=security.Name,
            Symbol=symbol,
            TradeTypeId=input.TradeTypeId,
            Quantity=input.TradeQuantity,
            IsMarket=tradeType.IsMarket,
            IsSell=tradeType.IsSell,
            StatusId=statusId,
        };
        if(status!=0){
            return output;
        }
        // Frame 4: 注文の記録(TRADE・TRADE_REQUEST・TRADE_HISTORY)。T_TAX には 0 を入れる。
        var now=input.Now;
        db.Trade.Insert(new TradeRow(input.TradeId,now,statusId,input.TradeTypeId,!input.TypeIsMargin,symbol,input.TradeQuantity,requestedPrice,input.AccountId,input.ExecFirstName+" "+input.ExecLastName,null,charge,commission,0m,input.IsLifo));
        if(!tradeType.IsMarket){
            db.TradeRequest.Insert(new TradeRequestRow(input.TradeId,input.TradeTypeId,symbol,input.TradeQuantity,requestedPrice,accountRow.BrokerId));
        }
        db.TradeHistory.Insert(new TradeHistoryRow(input.TradeId,now,statusId));
        // Frame 5: 意図的な巻き戻し(採番は戻さない)。
        if(input.RollItBack){
            db.Log.Rollback();
            return output with{RolledBack=true};
        }
        return output;
    }
    /// <summary>会社名と発行区分から銘柄を決める(索引 X16 は名前のハッシュ。衝突に備え名前を照合する)。</summary>
    private Code16 FindSymbolByCompany(TpcEDatabase db,string companyName,Code8 issue){
        foreach(var companyId in db.CompanyByName.All(NameHash.Of(companyName))){
            var company=db.Company.Get(new CompanyKey(companyId));
            if(company.Name!=companyName){
                continue;
            }
            foreach(var symbol in db.CompanySecurities.All(companyId)){
                if(db.Security.Get(new SecurityKey(symbol)).Issue==issue){
                    return symbol;
                }
            }
        }
        throw new InvalidOperationException("Company "+companyName+" with issue "+issue+" was not found.");
    }
}
