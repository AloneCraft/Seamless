namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Update(TPC-E 3.3.10)の手順。frame_to_execute のフレームを 1 つ実行する。LINQ は書かない(問い合わせは TradeUpdateQueries に書く)。
/// 仕様の擬似コードは 1 行ずつ「読む→変える→書く→読み直す」の順だが、ここでは新しい値を先に決めて出力に使い、
/// 書き込みは最後に主キー(取引 ID)の昇順でまとめて行う(1 つの取引で複数行を書くときの約束。結果の値と DB の変化は擬似コードと同じ)。
/// 文字列の切り替えはすべて大文字小文字を区別する(Ordinal)。
/// </summary>
internal sealed class TradeUpdateFrames{
    private const string ExecNameMarker=" X ";
    private const string CashAccount="Cash Account";
    private const string Cash="Cash";
    private const string MarginAccount="Margin Account";
    private const string Margin="Margin";
    private const string LowerSharesOf=" shares of ";
    private const string UpperSharesOf=" Shares of ";
    private readonly TradeUpdateQueries queries=new();
    public TradeUpdateOutput Run(TpcEDatabase db,TradeUpdateInput input){
        switch(input.FrameToExecute){
            case 1:
                return this.Frame1(db,input);
            case 2:
                return this.Frame2(db,input);
            case 3:
                return this.Frame3(db,input);
            default:
                throw new ArgumentOutOfRangeException(nameof(input),input.FrameToExecute,"frame_to_execute must be 1 to 3.");
        }
    }
    /// <summary>
    /// Frame 1(3.3.10.3): 指定の取引 ID ごとに、num_updated が max_updates 未満なら T_EXEC_NAME の " X " を付け外しし、
    /// TRADE × TRADE_TYPE・SETTLEMENT・CASH_TRANSACTION・TRADE_HISTORY を読む。num_found は変更する場合に読んだ行だけを数える(擬似コードどおり)。
    /// </summary>
    private TradeUpdateOutput Frame1(TpcEDatabase db,TradeUpdateInput input){
        var statusIds=this.queries.StatusIds(db);
        var count=Math.Min(Math.Max(0,input.MaxTrades),input.TradeIds.Length);
        var writes=new List<TradeUpdateWrite>(count);
        var trades=new List<TradeUpdateTrade>(count);
        var numFound=0;
        var numUpdated=0;
        for(var index=0;index<count;index++){
            if(!db.Trade.TryGet(new TradeKey(input.TradeIds[index]),out var trade)){
                // 行が無ければ読み取りも更新も 0 行。
                continue;
            }
            // 同じ取引 ID が前に出ていたら、その変更後の値から続ける。
            var execName=TradeUpdateFrames.PendingValue(writes,trade.Id,trade.ExecName);
            if(numUpdated<input.MaxUpdates){
                numFound++;
                execName=TradeUpdateFrames.ToggleExecName(execName);
                TradeUpdateFrames.SetPending(writes,trade.Id,execName);
                numUpdated++;
            }
            var isMarket=db.TradeType.Get(new TradeTypeKey(trade.TypeId)).IsMarket;
            trades.Add(TradeUpdateFrames.Build(trade with{ExecName=execName},null,null,isMarket,TradeUpdateFrames.ReadSettlement(db,trade.Id),TradeUpdateFrames.ReadCash(db,trade),this.queries.History(db,trade.Id,statusIds)));
        }
        foreach(var write in this.queries.SortWrites(db,writes.ToArray())){
            var row=db.Trade.Get(new TradeKey(write.TradeId));
            db.Trade.Update(row with{ExecName=write.Value});
        }
        var status=0;
        if(numFound!=input.MaxTrades){
            status=-1011;
        }
        if(numUpdated!=input.MaxUpdates){
            status=-1012;
        }
        return new TradeUpdateOutput{Status=status,FrameExecuted=1,NumFound=numFound,NumUpdated=numUpdated,Trades=trades.ToArray()};
    }
    /// <summary>
    /// Frame 2(3.3.10.4): 口座の期間内の取引を T_DTS の昇順に最大 max_trades 件読み、num_updated が max_updates 未満なら
    /// SE_CASH_TYPE を切り替え、決済・現金取引・履歴を読む。
    /// </summary>
    private TradeUpdateOutput Frame2(TpcEDatabase db,TradeUpdateInput input){
        var statusIds=this.queries.StatusIds(db);
        var rows=this.queries.AccountTrades(db,input.AccountId,input.StartTradeDts,input.EndTradeDts,Math.Max(0,input.MaxTrades));
        var writes=new List<TradeUpdateWrite>(rows.Length);
        var trades=new TradeUpdateTrade[rows.Length];
        var numUpdated=0;
        for(var index=0;index<rows.Length;index++){
            var trade=rows[index];
            TradeUpdateSettlement? settlement=null;
            if(db.Settlement.TryGet(new SettlementKey(trade.Id),out var settlementRow)){
                var cashType=settlementRow.CashType;
                if(numUpdated<input.MaxUpdates){
                    cashType=TradeUpdateFrames.ToggleCashType(trade.IsCash,cashType);
                    writes.Add(new TradeUpdateWrite(trade.Id,cashType));
                    numUpdated++;
                }
                settlement=new TradeUpdateSettlement(settlementRow.Amount,settlementRow.CashDueDate,cashType);
            }
            trades[index]=TradeUpdateFrames.Build(trade,null,null,false,settlement,TradeUpdateFrames.ReadCash(db,trade),this.queries.History(db,trade.Id,statusIds));
        }
        foreach(var write in this.queries.SortWrites(db,writes.ToArray())){
            var row=db.Settlement.Get(new SettlementKey(write.TradeId));
            db.Settlement.Update(row with{CashType=write.Value});
        }
        var numFound=rows.Length;
        var status=0;
        if(numFound<0||numFound>input.MaxTrades){
            status=-1021;
        }
        if(numUpdated==0){
            status=1021;
        }else if(numUpdated!=numFound){
            status=-1022;
        }
        return new TradeUpdateOutput{Status=status,FrameExecuted=2,NumFound=numFound,NumUpdated=numUpdated,Trades=trades};
    }
    /// <summary>
    /// Frame 3(3.3.10.5): 銘柄の期間内の取引を T_DTS の昇順に最大 max_trades 件読み(TRADE × TRADE_TYPE × SECURITY)、決済を読み、
    /// 現金取引なら num_updated が max_updates 未満のとき CT_NAME を作り直してから現金取引を読み、履歴を読む。
    /// </summary>
    private TradeUpdateOutput Frame3(TpcEDatabase db,TradeUpdateInput input){
        var statusIds=this.queries.StatusIds(db);
        var rows=this.queries.SymbolTrades(db,input.Symbol,input.StartTradeDts,input.EndTradeDts,Math.Max(0,input.MaxTrades));
        var writes=new List<TradeUpdateWrite>(rows.Length);
        var trades=new TradeUpdateTrade[rows.Length];
        var numUpdated=0;
        // SECURITY との結合(全行が同じ銘柄なので 1 回だけ読む)。
        var securityName="";
        if(rows.Length>0){
            securityName=db.Security.Get(new SecurityKey(input.Symbol)).Name;
        }
        for(var index=0;index<rows.Length;index++){
            var trade=rows[index];
            var typeName=db.TradeType.Get(new TradeTypeKey(trade.TypeId)).Name;
            var settlement=TradeUpdateFrames.ReadSettlement(db,trade.Id);
            TradeUpdateCash? cash=null;
            if(trade.IsCash&&db.CashTransaction.TryGet(new CashTransactionKey(trade.Id),out var cashRow)){
                var name=cashRow.Name;
                if(numUpdated<input.MaxUpdates){
                    name=TradeUpdateFrames.CashName(name,typeName,trade.Quantity,securityName);
                    writes.Add(new TradeUpdateWrite(trade.Id,name));
                    numUpdated++;
                }
                cash=new TradeUpdateCash(cashRow.Amount,cashRow.Dts,name);
            }
            trades[index]=TradeUpdateFrames.Build(trade,typeName,securityName,false,settlement,cash,this.queries.History(db,trade.Id,statusIds));
        }
        foreach(var write in this.queries.SortWrites(db,writes.ToArray())){
            var row=db.CashTransaction.Get(new CashTransactionKey(write.TradeId));
            db.CashTransaction.Update(row with{Name=write.Value});
        }
        var numFound=rows.Length;
        var status=0;
        if(numFound<0||numFound>input.MaxTrades){
            status=-1031;
        }
        if(numUpdated==0){
            status=1032;
        }else if(numUpdated>numFound){
            status=-1032;
        }
        return new TradeUpdateOutput{Status=status,FrameExecuted=3,NumFound=numFound,NumUpdated=numUpdated,Trades=trades};
    }
    /// <summary>T_EXEC_NAME の切り替え: " X " を含めば " X " を " " に、含まなければ " " を " X " に置き換える(どちらも全部)。</summary>
    private static string ToggleExecName(string execName){
        if(execName.Contains(TradeUpdateFrames.ExecNameMarker,StringComparison.Ordinal)){
            return execName.Replace(TradeUpdateFrames.ExecNameMarker," ",StringComparison.Ordinal);
        }
        return execName.Replace(" ",TradeUpdateFrames.ExecNameMarker,StringComparison.Ordinal);
    }
    /// <summary>SE_CASH_TYPE の切り替え: 現金取引は "Cash Account"↔"Cash"、信用取引は "Margin Account"↔"Margin"(それ以外の値は "～ Account" にする)。</summary>
    private static string ToggleCashType(bool isCash,string cashType){
        if(isCash){
            if(string.Equals(cashType,TradeUpdateFrames.CashAccount,StringComparison.Ordinal)){
                return TradeUpdateFrames.Cash;
            }
            return TradeUpdateFrames.CashAccount;
        }
        if(string.Equals(cashType,TradeUpdateFrames.MarginAccount,StringComparison.Ordinal)){
            return TradeUpdateFrames.Margin;
        }
        return TradeUpdateFrames.MarginAccount;
    }
    /// <summary>CT_NAME の作り直し: 今の値が " shares of " を含めば "種別名 数量 Shares of 銘柄名"、含まなければ "種別名 数量 shares of 銘柄名"。</summary>
    private static string CashName(string? current,string typeName,int quantity,string securityName){
        var quantityText=quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(current is not null&&current.Contains(TradeUpdateFrames.LowerSharesOf,StringComparison.Ordinal)){
            return typeName+" "+quantityText+TradeUpdateFrames.UpperSharesOf+securityName;
        }
        return typeName+" "+quantityText+TradeUpdateFrames.LowerSharesOf+securityName;
    }
    /// <summary>同じ取引 ID の変更待ちの値があればそれを、無ければ表の値を返す。</summary>
    private static string PendingValue(List<TradeUpdateWrite> writes,long tradeId,string current){
        foreach(var write in writes){
            if(write.TradeId==tradeId){
                return write.Value;
            }
        }
        return current;
    }
    /// <summary>変更待ちの値を置く(同じ取引 ID があれば上書きし、取引 ID を重ねない)。</summary>
    private static void SetPending(List<TradeUpdateWrite> writes,long tradeId,string value){
        for(var index=0;index<writes.Count;index++){
            if(writes[index].TradeId==tradeId){
                writes[index]=new TradeUpdateWrite(tradeId,value);
                return;
            }
        }
        writes.Add(new TradeUpdateWrite(tradeId,value));
    }
    private static TradeUpdateSettlement? ReadSettlement(TpcEDatabase db,long tradeId){
        if(db.Settlement.TryGet(new SettlementKey(tradeId),out var row)){
            return new TradeUpdateSettlement(row.Amount,row.CashDueDate,row.CashType);
        }
        return null;
    }
    /// <summary>現金取引のときだけ CASH_TRANSACTION を読む(行が無ければ null)。</summary>
    private static TradeUpdateCash? ReadCash(TpcEDatabase db,TradeRow trade){
        if(trade.IsCash&&db.CashTransaction.TryGet(new CashTransactionKey(trade.Id),out var row)){
            return new TradeUpdateCash(row.Amount,row.Dts,row.Name);
        }
        return null;
    }
    private static TradeUpdateTrade Build(TradeRow trade,string? typeName,string? securityName,bool isMarket,TradeUpdateSettlement? settlement,TradeUpdateCash? cash,TradeUpdateHistory[] history){
        return new TradeUpdateTrade(trade.Id,trade.AccountId,trade.Dts,trade.TypeId,typeName,securityName,trade.Quantity,trade.BidPrice,trade.ExecName,trade.IsCash,isMarket,trade.TradePrice,settlement,cash,history);
    }
}
