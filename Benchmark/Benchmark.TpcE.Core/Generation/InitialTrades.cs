namespace Benchmark.TpcE.Core;
/// <summary>
/// 初期取引の模擬。注文を時刻順に出し、約定を (時刻, 取引 ID) の順に処理して、Trade-Result と同じ式で行を作る(3.3.8.4〜3.3.8.8)。
/// 初期データには空売りを入れない(売りは、出した時点で売れる保有があるときだけ。無ければ買いに変える)。
/// 読み込みの終わりに約定していない注文は、未約定(PNDG)または提出済み(SBMT)のまま残す(Trade-Cleanup が片付ける)。
/// </summary>
internal sealed class InitialTrades{
    private readonly TpcEModel model;
    private readonly ILoadSink sink;
    private readonly decimal[] balanceDelta;
    private readonly long[] brokerTrades;
    private readonly decimal[] brokerCommission;
    private readonly long[] lastDayVolume;
    private readonly System.Collections.Generic.Dictionary<(long Account,long Security),Position> positions=[];
    private readonly PriorityQueue<Order,(long Time,long Id)> events=new();
    private readonly System.Collections.Generic.Dictionary<Code8,decimal> taxrates=[];
    private readonly long endTime;
    private readonly long lastDayStart;
    public InitialTrades(TpcEModel model,ILoadSink sink){
        this.model=model;
        this.sink=sink;
        this.balanceDelta=new decimal[model.Scale.Customers*10L+10];
        this.brokerTrades=new long[model.Scale.Brokers+1];
        this.brokerCommission=new decimal[model.Scale.Brokers+1];
        this.lastDayVolume=new long[model.Scale.Securities+1];
        for(var index=0;index<TpcEModel.TaxrateCount;index++){
            var row=FixedData.Taxrate(index);
            this.taxrates.Add(row.Id,row.Rate);
        }
        this.lastDayStart=TpcEModel.BusinessDayStart(Math.Max(0,model.Scale.InitialTradeDays-1));
        this.endTime=this.lastDayStart+TpcESpec.SecondsPerBusinessDay*1000L;
    }
    private sealed class Position{
        public readonly List<Hold> Holds=[];
        /// <summary>出したがまだ約定していない売りの数量(空売りを作らないため)。</summary>
        public long PendingSells;
        public long Quantity{
            get{
                long total=0;
                foreach(var hold in this.Holds){
                    total+=hold.Quantity;
                }
                return total;
            }
        }
    }
    private sealed class Hold(long tradeId,long dts,decimal price,int quantity){
        public long TradeId{get;}=tradeId;
        public long Dts{get;}=dts;
        public decimal Price{get;}=price;
        public int Quantity{get;set;}=quantity;
    }
    private sealed record Order(long Id,long Account,long Security,int TypeIndex,int Quantity,bool IsCash,bool Lifo,decimal BidPrice,string ExecName,long SubmitTime,long TriggerTime,long CompleteTime,decimal TradePrice);
    public GenerationResult Run(){
        var customers=this.model.Scale.Customers;
        var tradesPerDay=(long)TpcESpec.SecondsPerBusinessDay*customers/TpcESpec.ScaleFactor;
        var days=this.model.Scale.InitialTradeDays;
        long id=0;
        for(var day=0;day<days;day++){
            var dayStart=TpcEModel.BusinessDayStart(day);
            for(long n=0;n<tradesPerDay;n++){
                var time=dayStart+n*TpcESpec.SecondsPerBusinessDay*1000L/tradesPerDay;
                this.CompleteUntil(time);
                id++;
                this.Submit(id,time);
            }
        }
        this.CompleteUntil(this.endTime);
        // 約定しなかった注文を、未約定のまま残す。
        var firstUnfinished=id+1;
        var unfinished=0;
        while(this.events.TryDequeue(out var order,out _)){
            this.LeaveUnfinished(order);
            firstUnfinished=Math.Min(firstUnfinished,order.Id);
            unfinished++;
        }
        return new(id,TpcEModel.InitialTradeStart,this.endTime,firstUnfinished,unfinished);
    }
    private void CompleteUntil(long time){
        while(this.events.TryPeek(out var order,out var priority)&&priority.Time<=time){
            this.events.Dequeue();
            this.Complete(order);
        }
    }
    private void Submit(long id,long time){
        var rng=Rng.For(this.model.Seed,"initial-trade",id);
        var customer=this.model.PickCustomerByTier(ref rng);
        var account=this.model.PickAccountOf(customer,ref rng);
        var securities=this.model.SecuritiesOfAccount(account);
        var security=securities[rng.Range(0,securities.Length-1)];
        var typeIndex=rng.Weighted(FixedData.TradeTypeWeights);
        int[] quantities=[100,200,400,800];
        var quantity=quantities[rng.Range(0,3)];
        // 現金取引は買いの 84%・売りの 100%(2.6.1.8。全体では信用取引 8%)。種別が決まってから決める。
        var marginDraw=rng.Range(0,99);
        var lifo=rng.Percent(TpcESpec.TradeOrderLifoPercent);
        var person=0;
        var permissions=this.model.PermissionCountOf(account);
        if(permissions>=2&&rng.Percent(TpcESpec.TradeOrderThirdPartyPercent)){
            person=rng.Range(1,permissions-1);
        }
        var (_,lastName,firstName)=this.model.PermittedPerson(account,person);
        var execName=firstName+" "+lastName;
        var position=this.PositionOf(account,security);
        if(FixedData.TradeTypes[typeIndex].IsSell){
            if(position.Quantity-position.PendingSells<quantity){
                // 空売りを作らない: 売れる保有が無ければ買いに変える(成行→成行の買い、指値・逆指値→指値の買い)。
                if(FixedData.TradeTypes[typeIndex].IsMarket){
                    typeIndex=0;
                }else{
                    typeIndex=2;
                }
            }else{
                position.PendingSells+=quantity;
            }
        }
        var current=this.model.PriceCents(security,time);
        var type=FixedData.TradeTypes[typeIndex];
        var isCash=type.IsSell||marginDraw>=2*TpcESpec.TradeOrderMarginPercent;
        var delay=rng.Range(1000,5000);
        Order order;
        if(type.IsMarket){
            var complete=time+delay;
            order=new(id,account,security,typeIndex,quantity,isCash,lifo,current/100m,execName,time,time,complete,this.model.Price(security,complete));
        }else{
            long bid;
            // 指値は今の価格と下限・上限の間(端から 1.00 離す)。
            if(type.Id==FixedData.LimitSell){
                bid=rng.Range(Math.Min(current,TpcEModel.PriceHighCents-100),TpcEModel.PriceHighCents-100);
            }else{
                bid=rng.Range(TpcEModel.PriceLowCents+100,Math.Max(current,TpcEModel.PriceLowCents+100));
            }
            var trigger=this.TriggerTime(security,type.Id,bid,time+1000);
            order=new(id,account,security,typeIndex,quantity,isCash,lifo,bid/100m,execName,time,trigger,trigger+delay,bid/100m);
        }
        this.events.Enqueue(order,(order.CompleteTime,order.Id));
    }
    private long TriggerTime(long security,Code8 type,long bid,long from){
        var time=this.model.TriggerTime(security,type==FixedData.LimitSell,bid,from);
        if(time<0){
            throw new InvalidOperationException($"Limit price {bid} of security {security} is never reached.");
        }
        return time;
    }
    private Position PositionOf(long account,long security){
        if(!this.positions.TryGetValue((account,security),out var position)){
            position=new();
            this.positions.Add((account,security),position);
        }
        return position;
    }
    /// <summary>約定(Trade-Result の Frame 2〜6 と同じ式)。</summary>
    private void Complete(Order order){
        var type=FixedData.TradeTypes[order.TypeIndex];
        var customer=TpcEModel.CustomerOfAccount(order.Account);
        var tier=this.model.TierOf(customer);
        var symbol=this.model.SymbolOf(order.Security);
        var tradeDts=order.CompleteTime;
        var price=order.TradePrice;
        var position=this.PositionOf(order.Account,order.Security);
        if(type.IsSell){
            position.PendingSells-=order.Quantity;
        }
        // Frame 2: 保有の更新。
        var buyValue=0m;
        var sellValue=0m;
        var needed=order.Quantity;
        var holdQuantity=position.Quantity;
        var ordered=new List<Hold>(position.Holds);
        ordered.Sort(static (left,right)=>{
            var dts=left.Dts.CompareTo(right.Dts);
            if(dts!=0){
                return dts;
            }
            return left.TradeId.CompareTo(right.TradeId);
        });
        if(order.Lifo){
            ordered.Reverse();
        }
        if(type.IsSell){
            if(holdQuantity>0){
                foreach(var hold in ordered){
                    if(needed==0){
                        break;
                    }
                    if(hold.Quantity>needed){
                        this.sink.Load(new HoldingHistoryRow(hold.TradeId,order.Id,hold.Quantity,hold.Quantity-needed));
                        hold.Quantity-=needed;
                        buyValue+=needed*hold.Price;
                        sellValue+=needed*price;
                        needed=0;
                    }else{
                        this.sink.Load(new HoldingHistoryRow(hold.TradeId,order.Id,hold.Quantity,0));
                        position.Holds.Remove(hold);
                        buyValue+=hold.Quantity*hold.Price;
                        sellValue+=hold.Quantity*price;
                        needed-=hold.Quantity;
                    }
                }
            }
            if(needed>0){
                this.sink.Load(new HoldingHistoryRow(order.Id,order.Id,0,-needed));
                position.Holds.Add(new(order.Id,tradeDts,price,-needed));
            }
        }else{
            if(holdQuantity<0){
                foreach(var hold in ordered){
                    if(needed==0){
                        break;
                    }
                    if(hold.Quantity+needed<0){
                        this.sink.Load(new HoldingHistoryRow(hold.TradeId,order.Id,hold.Quantity,hold.Quantity+needed));
                        hold.Quantity+=needed;
                        sellValue+=needed*hold.Price;
                        buyValue+=needed*price;
                        needed=0;
                    }else{
                        this.sink.Load(new HoldingHistoryRow(hold.TradeId,order.Id,hold.Quantity,0));
                        position.Holds.Remove(hold);
                        var covered=-hold.Quantity;
                        sellValue+=covered*hold.Price;
                        buyValue+=covered*price;
                        needed-=covered;
                    }
                }
            }
            if(needed>0){
                this.sink.Load(new HoldingHistoryRow(order.Id,order.Id,0,needed));
                position.Holds.Add(new(order.Id,tradeDts,price,needed));
            }
        }
        // Frame 3: 税(税区分 1・2 で利益があるとき)。
        var taxStatus=this.model.TaxStatusOf(order.Account);
        var tax=0m;
        if((taxStatus==1||taxStatus==2)&&sellValue>buyValue){
            var rate=this.taxrates[this.model.NationalTaxrateOf(customer)]+this.taxrates[this.model.DivisionTaxrateOf(customer)];
            tax=decimal.Round((sellValue-buyValue)*rate,2,MidpointRounding.AwayFromZero);
        }
        // Frame 4・5: 手数料。
        var exchange=FixedData.ExchangeIndex(this.model.ExchangeOfSecurity(order.Security));
        var rateOfCommission=FixedData.CommissionRate(tier,order.TypeIndex,exchange,FixedData.CommissionBand(order.Quantity));
        var commission=decimal.Round(rateOfCommission/100m*(order.Quantity*price),2,MidpointRounding.AwayFromZero);
        var charge=FixedData.Charge(order.TypeIndex,tier);
        var broker=this.model.BrokerOfAccount(order.Account);
        this.brokerTrades[broker]++;
        this.brokerCommission[broker]+=commission;
        // Frame 6: 決済。
        decimal settlement;
        if(type.IsSell){
            settlement=order.Quantity*price-charge-commission;
        }else{
            settlement=-(order.Quantity*price+charge+commission);
        }
        if(taxStatus==1){
            settlement-=tax;
        }
        this.sink.Load(new TradeRow(order.Id,tradeDts,FixedData.Completed,type.Id,order.IsCash,symbol,order.Quantity,order.BidPrice,order.Account,order.ExecName,price,charge,commission,tax,order.Lifo));
        if(type.IsMarket){
            this.sink.Load(new TradeHistoryRow(order.Id,order.SubmitTime,FixedData.Submitted));
        }else{
            this.sink.Load(new TradeHistoryRow(order.Id,order.SubmitTime,FixedData.Pending));
            this.sink.Load(new TradeHistoryRow(order.Id,order.TriggerTime,FixedData.Submitted));
        }
        this.sink.Load(new TradeHistoryRow(order.Id,tradeDts,FixedData.Completed));
        string cashType;
        if(order.IsCash){
            cashType="Cash Account";
        }else{
            cashType="Margin";
        }
        this.sink.Load(new SettlementRow(order.Id,cashType,TpcETime.DateOf(tradeDts)+2,settlement));
        if(order.IsCash){
            this.balanceDelta[order.Account]+=settlement;
            this.sink.Load(new CashTransactionRow(order.Id,tradeDts,settlement,type.Name+" "+order.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)+" shares of "+this.model.SecurityName(order.Security)));
        }
        if(tradeDts>=this.lastDayStart){
            this.lastDayVolume[order.Security]+=order.Quantity;
        }
    }
    private void LeaveUnfinished(Order order){
        var type=FixedData.TradeTypes[order.TypeIndex];
        var symbol=this.model.SymbolOf(order.Security);
        var tier=this.model.TierOf(TpcEModel.CustomerOfAccount(order.Account));
        var charge=FixedData.Charge(order.TypeIndex,tier);
        if(!type.IsMarket&&order.TriggerTime>this.endTime){
            this.sink.Load(new TradeRow(order.Id,order.SubmitTime,FixedData.Pending,type.Id,order.IsCash,symbol,order.Quantity,order.BidPrice,order.Account,order.ExecName,null,charge,0m,0m,order.Lifo));
            this.sink.Load(new TradeHistoryRow(order.Id,order.SubmitTime,FixedData.Pending));
            this.sink.Load(new TradeRequestRow(order.Id,type.Id,symbol,order.Quantity,order.BidPrice,this.model.BrokerOfAccount(order.Account)));
            return;
        }
        long dts;
        if(type.IsMarket){
            dts=order.SubmitTime;
        }else{
            dts=order.TriggerTime;
            this.sink.Load(new TradeHistoryRow(order.Id,order.SubmitTime,FixedData.Pending));
        }
        this.sink.Load(new TradeRow(order.Id,dts,FixedData.Submitted,type.Id,order.IsCash,symbol,order.Quantity,order.BidPrice,order.Account,order.ExecName,null,charge,0m,0m,order.Lifo));
        this.sink.Load(new TradeHistoryRow(order.Id,dts,FixedData.Submitted));
    }
    /// <summary>約定の後に決まる行(保有、保有の要約、口座、ブローカー、最終約定)を出す。</summary>
    public void Finish(){
        var keys=new List<(long Account,long Security)>(this.positions.Keys);
        keys.Sort();
        foreach(var key in keys){
            var position=this.positions[key];
            var symbol=this.model.SymbolOf(key.Security);
            long total=0;
            foreach(var hold in position.Holds){
                this.sink.Load(new HoldingRow(hold.TradeId,key.Account,symbol,hold.Dts,hold.Price,hold.Quantity));
                total+=hold.Quantity;
            }
            if(total!=0){
                this.sink.Load(new HoldingSummaryRow(key.Account,symbol,checked((int)total)));
            }
        }
        string[] labels=["Individual","Joint","IRA","401(k)","Trust","Custodial","Business","Pension","Brokerage","Education"];
        for(var customer=1L;customer<=this.model.Scale.Customers;customer++){
            var name=this.model.FirstNameOf(customer)+" "+this.model.LastNameOf(customer);
            var accounts=this.model.AccountCountOf(customer);
            for(var index=0;index<accounts;index++){
                var account=TpcEModel.AccountId(customer,index);
                var initial=Rng.KeyedRange(this.model.Seed,"account-balance",account,0,1_000_000,100_000_000)/100m;
                this.sink.Load(new CustomerAccountRow(account,this.model.BrokerOfAccount(account),customer,name+" "+labels[index],this.model.TaxStatusOf(account),initial+this.balanceDelta[account]));
            }
        }
        for(var broker=1L;broker<=this.model.Scale.Brokers;broker++){
            this.sink.Load(new BrokerRow(broker,FixedData.Active,this.model.BrokerName(broker),this.brokerTrades[broker],this.brokerCommission[broker]));
        }
        for(var security=1L;security<=this.model.Scale.Securities;security++){
            this.sink.Load(new LastTradeRow(this.model.SymbolOf(security),this.endTime,this.model.Price(security,this.endTime),this.model.Price(security,this.lastDayStart),this.lastDayVolume[security]));
        }
    }
}
