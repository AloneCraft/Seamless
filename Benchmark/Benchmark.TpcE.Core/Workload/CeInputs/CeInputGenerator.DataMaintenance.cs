namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>vol_incr の絶対値の上限(推測: 仕様は「正または負の乱数」とだけ書く)。</summary>
    public const int DataMaintenanceMaxVolumeIncrement=100;
    /// <summary>
    /// Data-Maintenance の入力を作る(3.3.11.1)。tableIndex は 3.3.11 の一覧の順(0〜11、<see cref="DataMaintenanceInput.TableNames"/>)。
    /// 表ごとに使う引数だけを乱数で決め、使わない引数は 0 か空のままにする。
    /// </summary>
    public DataMaintenanceInput DataMaintenance(ref Rng rng,int tableIndex){
        if(tableIndex<0||tableIndex>=DataMaintenanceInput.TableNames.Count){
            throw new ArgumentOutOfRangeException(nameof(tableIndex),tableIndex,"Data-Maintenance has 12 tables (0..11).");
        }
        var tableName=DataMaintenanceInput.TableNames[tableIndex];
        var input=new DataMaintenanceInput{TableName=tableName,Now=this.Context.Now};
        switch(tableName){
        case DataMaintenanceInput.AccountPermission:{
            // 顧客を層で偏らせて選び、その顧客の口座から一様に 1 つ。
            var customer=this.Model.PickCustomerByTier(ref rng);
            return input with{AccountId=this.Model.PickAccountOf(customer,ref rng)};
        }
        case DataMaintenanceInput.Address:{
            // 67% は顧客の住所、33% は会社の住所。
            if(rng.Percent(67)){
                return input with{CustomerId=this.PickDataMaintenanceCustomer(ref rng)};
            }
            return input with{CompanyId=this.PickDataMaintenanceCompany(ref rng)};
        }
        case DataMaintenanceInput.Company:
        case DataMaintenanceInput.Financial:
        case DataMaintenanceInput.NewsItem:
            return input with{CompanyId=this.PickDataMaintenanceCompany(ref rng)};
        case DataMaintenanceInput.Customer:
        case DataMaintenanceInput.CustomerTaxrate:
        case DataMaintenanceInput.WatchItem:
            return input with{CustomerId=this.PickDataMaintenanceCustomer(ref rng)};
        case DataMaintenanceInput.DailyMarket:{
            var symbol=this.PickDataMaintenanceSymbol(ref rng);
            var day=rng.Range(1,31);
            // 推測: -100〜100 の 0 以外を一様に。
            var increment=rng.Range(-CeInputGenerator.DataMaintenanceMaxVolumeIncrement,CeInputGenerator.DataMaintenanceMaxVolumeIncrement-1);
            if(increment>=0){
                increment++;
            }
            return input with{Symbol=symbol,DayOfMonth=day,VolumeIncrement=increment};
        }
        case DataMaintenanceInput.Exchange:
            return input;
        case DataMaintenanceInput.Security:
            return input with{Symbol=this.PickDataMaintenanceSymbol(ref rng)};
        case DataMaintenanceInput.Taxrate:{
            // 推測: TAXRATE の全 ID(国 9 個 + 地域)から一様に。
            var index=rng.Range(0,TpcEModel.TaxrateCount-1);
            Code8 taxId;
            if(index<TpcEModel.NationalTaxrates.Length){
                taxId=Code8.From(TpcEModel.NationalTaxrates[index]);
            }else{
                taxId=TpcEModel.DivisionTaxrateId(index-TpcEModel.NationalTaxrates.Length);
            }
            return input with{TaxId=taxId};
        }
        default:
            throw new InvalidOperationException($"Unknown Data-Maintenance table '{tableName}'.");
        }
    }
    /// <summary>c_id は顧客全体から一様(3.3.11.1)。</summary>
    private long PickDataMaintenanceCustomer(ref Rng rng)=>rng.Range(1,this.Model.CustomerCount);
    /// <summary>co_id は会社全体から一様(3.3.11.1)。</summary>
    private long PickDataMaintenanceCompany(ref Rng rng)=>rng.Range(1,this.Model.Scale.Companies);
    /// <summary>symbol の分布は仕様書に記載なし(推測: 銘柄全体から一様)。</summary>
    private Code16 PickDataMaintenanceSymbol(ref Rng rng)=>this.Model.SymbolOf(rng.Range(1,this.Model.Scale.Securities));
}
