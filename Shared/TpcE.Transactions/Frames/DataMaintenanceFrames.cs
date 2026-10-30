namespace Shared.TpcE.Transactions;
/// <summary>
/// Data-Maintenance(TPC-E 3.3.11)の手順。フレームは 1 つで、入力の table_name の表を 1 つだけ書き換える。
/// LINQ は書かない(問い合わせは DataMaintenanceQueries に書く)。仕様に検査が無いので status は常に 0。
/// 仕様書に書かれていない場合(該当する行が無いなど)は何も書かずに終える。
/// </summary>
internal sealed class DataMaintenanceFrames{
    private const string ApartmentTenC="Apt. 10C";
    private const string ApartmentTwentyTwo="Apt. 22";
    private const string SpRateAba="ABA";
    private const string SpRateAaa="AAA";
    private const string MindspringSuffix="@mindspring.com";
    private const string MindspringHost="mindspring.com";
    private const string EarthlinkHost="earthlink.com";
    private const string LastUpdated=" LAST UPDATED ";
    private const string UpperTax=" Tax ";
    private const string LowerTax=" tax ";
    /// <summary>FINANCIAL は 2000-01-01 開始の四半期から 2004-10-01 開始の四半期までの 20 行(2.2.6.5)。FI_YEAR は四半期末の年。</summary>
    private const long FirstFinancialYear=2000;
    private const long LastFinancialYear=2004;
    private const long QuartersPerYear=4;
    private static readonly Code8 AclFull=Code8.From("1111");
    private static readonly Code8 AclReduced=Code8.From("0011");
    private readonly DataMaintenanceQueries queries=new();
    public DataMaintenanceOutput Run(TpcEDatabase db,DataMaintenanceInput input){
        int updated;
        switch(input.TableName){
        case DataMaintenanceInput.AccountPermission:
            updated=this.UpdateAccountPermission(db,input);
            break;
        case DataMaintenanceInput.Address:
            updated=this.UpdateAddress(db,input);
            break;
        case DataMaintenanceInput.Company:
            updated=this.UpdateCompany(db,input);
            break;
        case DataMaintenanceInput.Customer:
            updated=this.UpdateCustomer(db,input);
            break;
        case DataMaintenanceInput.CustomerTaxrate:
            updated=this.UpdateCustomerTaxrate(db,input);
            break;
        case DataMaintenanceInput.DailyMarket:
            updated=this.UpdateDailyMarket(db,input);
            break;
        case DataMaintenanceInput.Exchange:
            updated=this.UpdateExchange(db,input);
            break;
        case DataMaintenanceInput.Financial:
            updated=this.UpdateFinancial(db,input);
            break;
        case DataMaintenanceInput.NewsItem:
            updated=this.UpdateNewsItem(db,input);
            break;
        case DataMaintenanceInput.Security:
            updated=this.UpdateSecurity(db,input);
            break;
        case DataMaintenanceInput.Taxrate:
            updated=this.UpdateTaxrate(db,input);
            break;
        case DataMaintenanceInput.WatchItem:
            updated=this.UpdateWatchItem(db,input);
            break;
        default:
            throw new ArgumentException($"Unknown Data-Maintenance table '{input.TableName}'.",nameof(input));
        }
        return new DataMaintenanceOutput{Status=0,NumUpdated=updated};
    }
    /// <summary>口座の権限の AP_ACL を "1111" と "0011" で切り替える(先頭の行は AP_ACL の降順、その値を持つ行を全部変える)。</summary>
    private int UpdateAccountPermission(TpcEDatabase db,DataMaintenanceInput input){
        var acl=this.queries.TopAccountPermissionAcl(db,input.AccountId);
        Code8 next;
        if(acl==DataMaintenanceFrames.AclFull){
            next=DataMaintenanceFrames.AclReduced;
        }else{
            next=DataMaintenanceFrames.AclFull;
        }
        var rows=this.queries.AccountPermissionsWithAcl(db,input.AccountId,acl);
        foreach(var row in rows){
            db.AccountPermission.Update(row with{Acl=next});
        }
        return rows.Length;
    }
    /// <summary>顧客(c_id が 0 でないとき)か会社の住所の AD_LINE2 を "Apt. 10C" と "Apt. 22" で切り替える。NULL は "Apt. 10C" 以外として扱う。</summary>
    private int UpdateAddress(TpcEDatabase db,DataMaintenanceInput input){
        long addressId;
        if(input.CustomerId!=0){
            addressId=db.Customer.Get(new CustomerKey(input.CustomerId)).AddressId;
        }else{
            addressId=db.Company.Get(new CompanyKey(input.CompanyId)).AddressId;
        }
        var address=db.Address.Get(new AddressKey(addressId));
        string line2;
        if(string.Equals(address.Line2,DataMaintenanceFrames.ApartmentTenC,StringComparison.Ordinal)){
            line2=DataMaintenanceFrames.ApartmentTwentyTwo;
        }else{
            line2=DataMaintenanceFrames.ApartmentTenC;
        }
        db.Address.Update(address with{Line2=line2});
        return 1;
    }
    /// <summary>会社の CO_SP_RATE を "ABA" と "AAA" で切り替える。</summary>
    private int UpdateCompany(TpcEDatabase db,DataMaintenanceInput input){
        var company=db.Company.Get(new CompanyKey(input.CompanyId));
        string rate;
        if(string.Equals(company.SpRate,DataMaintenanceFrames.SpRateAba,StringComparison.Ordinal)){
            rate=DataMaintenanceFrames.SpRateAaa;
        }else{
            rate=DataMaintenanceFrames.SpRateAba;
        }
        db.Company.Update(company with{SpRate=rate});
        return 1;
    }
    /// <summary>
    /// 顧客の C_EMAIL_2 の「@」より後ろを "mindspring.com" と "earthlink.com" で切り替える。
    /// 擬似コードの式どおり、前半は先頭から最初の「@」まで(「@」が無ければ空)。NULL は NULL のまま(NULL に文字を足しても NULL)。
    /// </summary>
    private int UpdateCustomer(TpcEDatabase db,DataMaintenanceInput input){
        var customer=db.Customer.Get(new CustomerKey(input.CustomerId));
        var email=customer.Email2;
        string? next=null;
        if(email is not null){
            var head=email[..(email.IndexOf('@',StringComparison.Ordinal)+1)];
            if(email.Length>DataMaintenanceFrames.MindspringSuffix.Length&&email.EndsWith(DataMaintenanceFrames.MindspringSuffix,StringComparison.Ordinal)){
                next=head+DataMaintenanceFrames.EarthlinkHost;
            }else{
                next=head+DataMaintenanceFrames.MindspringHost;
            }
        }
        db.Customer.Update(customer with{Email2=next});
        return 1;
    }
    /// <summary>顧客の国の税率を US1→…→US5→US1、CN1→…→CN4→CN1 と巡回させる。主キーが変わるので消して足す。</summary>
    private int UpdateCustomerTaxrate(TpcEDatabase db,DataMaintenanceInput input){
        var old=this.queries.CustomerCountryTaxrate(db,input.CustomerId);
        var next=DataMaintenanceFrames.NextCountryTaxrate(old);
        var oldKey=new CustomerTaxrateKey(old,input.CustomerId);
        var nextRow=new CustomerTaxrateRow(next,input.CustomerId);
        // 主キー (CX_TX_ID, CX_C_ID) の昇順に書く。
        if(next.CompareTo(old)<0){
            db.CustomerTaxrate.Insert(nextRow);
            db.CustomerTaxrate.Delete(oldKey);
        }else{
            db.CustomerTaxrate.Delete(oldKey);
            db.CustomerTaxrate.Insert(nextRow);
        }
        return 1;
    }
    /// <summary>擬似コードどおり、"US5" と "CN4" は 1 に戻し、それ以外は末尾の数字に 1 を足す("US" でなければ CN の組)。</summary>
    private static Code8 NextCountryTaxrate(Code8 old){
        var text=old.ToString();
        if(text.StartsWith("US",StringComparison.Ordinal)){
            if(string.Equals(text,"US5",StringComparison.Ordinal)){
                return Code8.From("US1");
            }
            return Code8.From("US"+(char)(text[^1]+1));
        }
        if(string.Equals(text,"CN4",StringComparison.Ordinal)){
            return Code8.From("CN1");
        }
        return Code8.From("CN"+(char)(text[^1]+1));
    }
    /// <summary>銘柄の DAILY_MARKET のうち、日付の「日」が day_of_month の行全部の DM_VOL に vol_incr を足す。</summary>
    private int UpdateDailyMarket(TpcEDatabase db,DataMaintenanceInput input){
        var keys=DataMaintenanceFrames.DailyMarketKeys(input.Symbol,input.DayOfMonth,db.Scale.MarketDays);
        var rows=this.queries.DailyMarketRows(db,keys);
        foreach(var row in rows){
            db.DailyMarket.Update(row with{Volume=row.Volume+input.VolumeIncrement});
        }
        return rows.Length;
    }
    /// <summary>
    /// DAILY_MARKET の主キーの候補(日付の昇順)。DAILY_MARKET は <see cref="TpcEModel.LastMarketDay"/>(2004-12-31)で終わる
    /// marketDays 日の平日を持つ(<see cref="TpcEModel.MarketDays"/> と同じ並び)ので、その中で日が dayOfMonth のものだけを当てる。
    /// </summary>
    private static DailyMarketKey[] DailyMarketKeys(Code16 symbol,int dayOfMonth,int marketDays){
        var days=new List<long>();
        var day=TpcEModel.LastMarketDay;
        var remaining=marketDays;
        while(remaining>0){
            var date=TpcETime.ToDate(day);
            if(date.DayOfWeek!=DayOfWeek.Saturday&&date.DayOfWeek!=DayOfWeek.Sunday){
                if(date.Day==dayOfMonth){
                    days.Add(day);
                }
                remaining--;
            }
            day--;
        }
        var keys=new DailyMarketKey[days.Count];
        for(var index=0;index<keys.Length;index++){
            keys[index]=new DailyMarketKey(symbol,days[days.Count-1-index]);
        }
        return keys;
    }
    /// <summary>
    /// 取引所の EX_DESC の末尾に " LAST UPDATED " と現在日時を付ける。どれかに付いていれば、全行の末尾の日時を現在日時に差し替える。
    /// 日時の書式は <see cref="TpcETime.Format"/>。NULL は NULL のまま。
    /// </summary>
    private int UpdateExchange(TpcEDatabase db,DataMaintenanceInput input){
        var updatedCount=this.queries.ExchangeUpdatedCount(db);
        var rows=this.queries.ExchangeRows(db);
        var now=TpcETime.Format(input.Now);
        foreach(var row in rows){
            string? description=null;
            if(row.Description is not null){
                if(updatedCount==0){
                    description=row.Description+DataMaintenanceFrames.LastUpdated+now;
                }else{
                    // 末尾の日時(現在日時と同じ長さ)を差し替える。短すぎる説明は全体を差し替える。
                    description=row.Description[..Math.Max(0,row.Description.Length-now.Length)]+now;
                }
            }
            db.Exchange.Update(row with{Description=description});
        }
        return rows.Length;
    }
    /// <summary>会社の FINANCIAL の四半期開始日を、1 日のものが 1 行でもあれば全行 +1 日、無ければ全行 -1 日する。</summary>
    private int UpdateFinancial(TpcEDatabase db,DataMaintenanceInput input){
        var keys=new FinancialKey[(DataMaintenanceFrames.LastFinancialYear-DataMaintenanceFrames.FirstFinancialYear+1)*DataMaintenanceFrames.QuartersPerYear];
        var position=0;
        for(var year=DataMaintenanceFrames.FirstFinancialYear;year<=DataMaintenanceFrames.LastFinancialYear;year++){
            for(var quarter=1L;quarter<=DataMaintenanceFrames.QuartersPerYear;quarter++){
                keys[position++]=new FinancialKey(input.CompanyId,year,quarter);
            }
        }
        var rows=this.queries.FinancialRows(db,keys);
        var firstDays=this.queries.FinancialFirstDayCount(db,rows);
        long shift;
        if(firstDays>0){
            shift=1;
        }else{
            shift=-1;
        }
        foreach(var row in rows){
            db.Financial.Update(row with{QuarterStartDate=row.QuarterStartDate+shift});
        }
        return rows.Length;
    }
    /// <summary>会社のニュース全部の NI_DTS を 1 日進める。</summary>
    private int UpdateNewsItem(TpcEDatabase db,DataMaintenanceInput input){
        var items=this.queries.CompanyNewsItems(db,input.CompanyId);
        foreach(var item in items){
            db.NewsItem.Update(item with{Dts=item.Dts+TpcETime.MillisecondsPerDay});
        }
        return items.Length;
    }
    /// <summary>銘柄の S_EXCH_DATE を 1 日進める。</summary>
    private int UpdateSecurity(TpcEDatabase db,DataMaintenanceInput input){
        var security=db.Security.Get(new SecurityKey(input.Symbol));
        db.Security.Update(security with{ExchangeDate=security.ExchangeDate+1});
        return 1;
    }
    /// <summary>税率の TX_NAME の " Tax " と " tax " を切り替える(大文字小文字を区別する)。</summary>
    private int UpdateTaxrate(TpcEDatabase db,DataMaintenanceInput input){
        var taxrate=db.Taxrate.Get(new TaxrateKey(input.TaxId));
        string name;
        if(taxrate.Name.Contains(DataMaintenanceFrames.UpperTax,StringComparison.Ordinal)){
            name=taxrate.Name.Replace(DataMaintenanceFrames.UpperTax,DataMaintenanceFrames.LowerTax,StringComparison.Ordinal);
        }else{
            name=taxrate.Name.Replace(DataMaintenanceFrames.LowerTax,DataMaintenanceFrames.UpperTax,StringComparison.Ordinal);
        }
        db.Taxrate.Update(taxrate with{Name=name});
        return 1;
    }
    /// <summary>
    /// 顧客のウォッチ銘柄を記号順に並べた「中央」(ROWNUM = (件数+1)/2、1 始まり)の銘柄を、SECURITY で次に来る未登録の銘柄に差し替える。
    /// 主キーが変わるので消して足す。ウォッチ銘柄が無いときと、次の銘柄が無いときは何もしない(仕様書に記載なし)。
    /// </summary>
    private int UpdateWatchItem(TpcEDatabase db,DataMaintenanceInput input){
        var watchLists=db.CustomerWatchLists.All(input.CustomerId);
        var symbols=this.queries.WatchSymbols(db,watchLists);
        if(symbols.Length==0){
            return 0;
        }
        var oldSymbol=symbols[(symbols.Length+1)/2-1];
        var candidates=this.queries.NextUnwatchedSymbol(db,oldSymbol,symbols);
        if(candidates.Length==0){
            return 0;
        }
        var newSymbol=candidates[0];
        var updated=0;
        foreach(var watchListId in watchLists){
            var oldKey=new WatchItemKey(watchListId,oldSymbol);
            if(!db.WatchItem.Contains(oldKey)){
                continue;
            }
            // 主キー (WI_WL_ID, WI_S_SYMB) の昇順に書く。新しい銘柄は古い銘柄より後ろなので、消してから足す。
            db.WatchItem.Delete(oldKey);
            db.WatchItem.Insert(new WatchItemRow(watchListId,newSymbol));
            updated++;
        }
        return updated;
    }
}
