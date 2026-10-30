namespace Shared.TpcE.Transactions;
/// <summary>Frame 3 の見積もり(3.3.7.5)。書き込みはしない。</summary>
internal readonly record struct TradeOrderEstimate(
    Code16 Symbol,string SecurityName,decimal MarketPrice,decimal RequestedPrice,decimal BuyValue,decimal SellValue,decimal TaxAmount,
    decimal CommissionRate,decimal Charge,decimal Assets,bool IsMarket,bool IsSell,Code8 StatusId);
/// <summary>
/// Trade-Order(TPC-E 3.3.7)の手順。LINQ は書かない(問い合わせは TradeOrderQueries に書く)。
/// 仕様のフレーム 1〜4 を別の関数にし、<see cref="Run"/> はそれらを順に呼ぶ(EGenTxnHarness は同じフレームを自分の順序で呼ぶ)。
/// </summary>
internal sealed class TradeOrderFrames{
    private readonly TradeOrderQueries queries=new();
    public TradeOrderOutput Run(TpcEDatabase db,TradeOrderInput input){
        // Frame 1: 口座・顧客。
        if(!this.Frame1(db,input.AccountId,out var accountRow,out var customer)){
            return new TradeOrderOutput{Status=-711,TradeId=input.TradeId};
        }
        // Frame 2: 実行者が持ち主でなければ権限を確かめる。
        if(input.ExecLastName!=customer.LastName||input.ExecFirstName!=customer.FirstName||input.ExecTaxId!=customer.TaxId){
            if(!this.Frame2(db,input.AccountId,input.ExecFirstName,input.ExecLastName,input.ExecTaxId)){
                return new TradeOrderOutput{Status=-721,TradeId=input.TradeId};
            }
        }
        // Frame 3: 銘柄の特定と見積もり。
        var estimate=this.Frame3(db,input.AccountId,accountRow.CustomerId,customer.Tier,accountRow.TaxStatus,input.IsLifo,input.Issue,input.TradeQuantity,input.TradeTypeId,input.TypeIsMargin,input.CompanyName,input.RequestedPrice,input.Symbol,FixedData.Pending,FixedData.Submitted);
        var taxed=accountRow.TaxStatus==1||accountRow.TaxStatus==2;
        var status=0;
        if(estimate.SellValue>estimate.BuyValue&&taxed&&estimate.TaxAmount<=0m){
            status=-731;
        }else if(estimate.CommissionRate<=0m){
            status=-732;
        }else if(estimate.Charge==0m){
            status=-733;
        }
        var commission=decimal.Round(estimate.CommissionRate/100m*input.TradeQuantity*estimate.RequestedPrice,2,MidpointRounding.AwayFromZero);
        var output=new TradeOrderOutput{
            Status=status,
            TradeId=input.TradeId,
            BuyValue=estimate.BuyValue,
            SellValue=estimate.SellValue,
            TaxAmount=estimate.TaxAmount,
            CommissionAmount=commission,
            ChargeAmount=estimate.Charge,
            MarketPrice=estimate.MarketPrice,
            RequestedPrice=estimate.RequestedPrice,
            AccountAssets=estimate.Assets,
            SecurityName=estimate.SecurityName,
            Symbol=estimate.Symbol,
            TradeTypeId=input.TradeTypeId,
            Quantity=input.TradeQuantity,
            IsMarket=estimate.IsMarket,
            IsSell=estimate.IsSell,
            StatusId=estimate.StatusId,
        };
        if(status!=0){
            return output;
        }
        // Frame 4: 注文の記録(TRADE・TRADE_REQUEST・TRADE_HISTORY)。
        this.Frame4(db,input.TradeId,input.AccountId,accountRow.BrokerId,estimate.Charge,commission,input.ExecFirstName+" "+input.ExecLastName,!input.TypeIsMargin,input.IsLifo,estimate.RequestedPrice,estimate.StatusId,estimate.Symbol,input.TradeQuantity,input.TradeTypeId,estimate.IsMarket,input.Now);
        // Frame 5: 意図的な巻き戻し(採番は戻さない)。
        if(input.RollItBack){
            db.Log.Rollback();
            return output with{RolledBack=true};
        }
        return output;
    }
    /// <summary>Frame 1(3.3.7.3)。口座とその持ち主。口座が無ければ false(num_found=0)。</summary>
    public bool Frame1(TpcEDatabase db,long accountId,out CustomerAccountRow account,out CustomerRow customer){
        var found=db.CustomerAccount.Find(new CustomerAccountKey(accountId));
        if(found is not CustomerAccountRow accountRow){
            account=default;
            customer=default;
            return false;
        }
        account=accountRow;
        customer=db.Customer.Get(new CustomerKey(accountRow.CustomerId));
        return true;
    }
    /// <summary>Frame 2(3.3.7.4)。実行者に口座の権限があるか(ACCOUNT_PERMISSION に口座・納税者番号が当たり、名前も合う)。</summary>
    public bool Frame2(TpcEDatabase db,long accountId,string firstName,string lastName,Code24 taxId){
        var permission=db.AccountPermission.Find(new AccountPermissionKey(accountId,taxId));
        if(permission is AccountPermissionRow permissionRow){
            return permissionRow.FirstName==firstName&&permissionRow.LastName==lastName;
        }
        return false;
    }
    /// <summary>Frame 3(3.3.7.5)。銘柄の特定、市場価格、保有を取り崩した場合の取得額と売却額、税、手数料率、手数料(Charge)、資産。</summary>
    public TradeOrderEstimate Frame3(TpcEDatabase db,long accountId,long customerId,int tier,int taxStatus,bool isLifo,Code8 issue,int quantity,Code8 tradeTypeId,bool typeIsMargin,string companyName,decimal requestedPriceInput,Code16 symbolInput,Code8 pendingId,Code8 submittedId){
        var symbol=symbolInput;
        if(companyName.Length>0){
            symbol=this.FindSymbolByCompany(db,companyName,issue);
        }
        var security=db.Security.Get(new SecurityKey(symbol));
        var market=db.LastTrade.Get(new LastTradeKey(symbol)).Price;
        var tradeType=db.TradeType.Get(new TradeTypeKey(tradeTypeId));
        var requestedPrice=requestedPriceInput;
        if(tradeType.IsMarket){
            requestedPrice=market;
        }
        // 既存の保有を LIFO/FIFO で取り崩した場合の取得額と売却額(見積もり。書かない)。
        var buyValue=0m;
        var sellValue=0m;
        var needed=quantity;
        long summaryQuantity=0;
        var summary=db.HoldingSummary.Find(new HoldingSummaryKey(accountId,symbol));
        if(summary is HoldingSummaryRow summaryRow){
            summaryQuantity=summaryRow.Quantity;
        }
        if((tradeType.IsSell&&summaryQuantity>0)||(!tradeType.IsSell&&summaryQuantity<0)){
            foreach(var hold in HoldingCursor.Read(db,accountId,symbol,isLifo)){
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
        var taxed=taxStatus==1||taxStatus==2;
        if(sellValue>buyValue&&taxed){
            var rates=this.queries.TaxRateSum(db,db.CustomerTaxrates.All(customerId));
            taxAmount=decimal.Round((sellValue-buyValue)*rates,2,MidpointRounding.AwayFromZero);
        }
        var commissionRate=this.queries.CommissionRate(db,tier,tradeTypeId,security.ExchangeId,quantity);
        var charge=db.Charge.Get(new ChargeKey(tradeTypeId,tier)).Charge;
        var assets=0m;
        if(typeIsMargin){
            var account=db.CustomerAccount.Get(new CustomerAccountKey(accountId));
            assets=account.Balance+this.queries.HoldingAssets(db,accountId,db.AccountHoldingSummaries.All(accountId));
        }
        var statusId=pendingId;
        if(tradeType.IsMarket){
            statusId=submittedId;
        }
        return new TradeOrderEstimate(symbol,security.Name,market,requestedPrice,buyValue,sellValue,taxAmount,commissionRate,charge,assets,tradeType.IsMarket,tradeType.IsSell,statusId);
    }
    /// <summary>Frame 4(3.3.7.6)。注文の記録(TRADE・TRADE_REQUEST・TRADE_HISTORY)。T_TAX には 0 を入れる。</summary>
    public void Frame4(TpcEDatabase db,long tradeId,long accountId,long brokerId,decimal charge,decimal commission,string execName,bool isCash,bool isLifo,decimal requestedPrice,Code8 statusId,Code16 symbol,int quantity,Code8 tradeTypeId,bool isMarket,long now){
        db.Trade.Insert(new TradeRow(tradeId,now,statusId,tradeTypeId,isCash,symbol,quantity,requestedPrice,accountId,execName,null,charge,commission,0m,isLifo));
        if(!isMarket){
            db.TradeRequest.Insert(new TradeRequestRow(tradeId,tradeTypeId,symbol,quantity,requestedPrice,brokerId));
        }
        db.TradeHistory.Insert(new TradeHistoryRow(tradeId,now,statusId));
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
