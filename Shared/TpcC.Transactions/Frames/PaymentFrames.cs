using System.Globalization;
namespace Shared.TpcC.Transactions;
/// <summary>
/// Payment(仕様 2.5.2、HammerDB の payment は QUIRKS Q-034〜Q-039)。
/// 顧客の区画に顧客の行を、家の倉庫の区画に履歴・地区・倉庫を書く(顧客が他の倉庫なら 2 つの区画にまたがる)。
/// </summary>
internal sealed class PaymentFrames(TpcCMode mode,CustomerLookup lookup){
    private const int CustomerDataLength=500;
    public PaymentResult Run(TpcCDatabase db,PaymentRequest request){
        var homeWarehouse=request.WarehouseId;
        var home=db.For(homeWarehouse);
        var customerPartition=db.For(request.CustomerWarehouseId);
        var customerId=lookup.Resolve(customerPartition,request.CustomerWarehouseId,request.CustomerDistrictId,request.CustomerId,request.CustomerLast);
        var customer=customerPartition.Customer.Get(new CustomerKey(request.CustomerWarehouseId,request.CustomerDistrictId,customerId));
        var warehouse=home.Warehouse.Get(new WarehouseKey(homeWarehouse));
        var district=home.District.Get(new DistrictKey(homeWarehouse,request.DistrictId));
        var updated=customer with{Balance=customer.Balance-request.Amount};
        if(mode==TpcCMode.Spec){
            updated=updated with{YtdPayment=customer.YtdPayment+request.Amount,PaymentCount=customer.PaymentCount+1};
            if(customer.Credit=="BC"){
                var prefix=string.Join(" ",customerId,request.CustomerDistrictId,request.CustomerWarehouseId,request.DistrictId,homeWarehouse,request.Amount.ToString(CultureInfo.InvariantCulture))+" ";
                updated=updated with{Data=Truncate(prefix+customer.Data)};
            }
        }else{
            // HammerDB: C_YTD_PAYMENT・C_PAYMENT_CNT は更新しない(Q-035)。
            if(customer.Credit=="BC"){
                updated=updated with{Data=Truncate(HammerDbPrefix(customerId,request,customer)+customer.Data)};
            }else{
                // 信用が BC でない顧客も C_DATA を信用の値で上書きしてしまう(Q-036)。列は char(500) なので空白で 500 文字に詰まる(行は縮まない)。
                updated=updated with{Data=customer.Credit.PadRight(CustomerDataLength)};
            }
        }
        customerPartition.Customer.Update(updated);
        string historyData;
        if(mode==TpcCMode.Spec){
            historyData=warehouse.Name+"    "+district.Name;
        }else{
            // 倉庫名と地区名をまだ読んでいない時点で H_DATA を作るので、空白だけになる(Q-038。列は 24 文字)。
            historyData=new string(' ',24);
        }
        var historyId=district.NextHistoryId;
        home.History.Insert(new HistoryRow(homeWarehouse,request.DistrictId,historyId,request.CustomerWarehouseId,request.CustomerDistrictId,customerId,request.Now,request.Amount,historyData));
        home.District.Update(district with{Ytd=district.Ytd+request.Amount,NextHistoryId=historyId+1});
        home.Warehouse.Update(warehouse with{Ytd=warehouse.Ytd+request.Amount});
        // 出力は仕様 2.5.3.3 の項目。C_DATA は仕様の形で信用が BC のときだけ、更新後の先頭 200 文字(HammerDB の手続きは返さない)。
        var preview="";
        if(mode==TpcCMode.Spec&&updated.Credit=="BC"){
            preview=updated.Data[..Math.Min(200,updated.Data.Length)];
        }
        return new PaymentResult(false,customerId,customer.First,customer.Middle,customer.Last,customer.Street1,customer.Street2,customer.City,customer.State,customer.Zip,customer.Phone,
            customer.Since,customer.Credit,customer.CreditLimit,customer.Discount,updated.Balance,preview,historyId,
            warehouse.Name,warehouse.Street1,warehouse.Street2,warehouse.City,warehouse.State,warehouse.Zip,
            district.Name,district.Street1,district.Street2,district.City,district.State,district.Zip);
    }
    /// <summary>
    /// HammerDB の BC 顧客の C_DATA の先頭(Q-037): 顧客番号・顧客の地区・顧客の倉庫・地区・倉庫(それぞれ 30 文字に空白で詰める)、支払額(8 文字)、日時(30 文字)、
    /// 倉庫名と地区名(まだ読んでいないので空白 11 文字ずつ)。各項目の後ろに空白 1 つで、合計 219 文字。
    /// </summary>
    internal static string HammerDbPrefix(int customerId,PaymentRequest request,CustomerRow customer){
        // 支払額は numeric(6,2) を char(8) にするので小数 2 桁('1.00    '、'5000.00 ')。
        var amount=request.Amount.ToString("0.00",CultureInfo.InvariantCulture);
        var date=DateTimeOffset.FromUnixTimeSeconds(request.Now).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        return Pad(customerId.ToString(CultureInfo.InvariantCulture),30)+" "
            +Pad(request.CustomerDistrictId.ToString(CultureInfo.InvariantCulture),30)+" "
            +Pad(request.CustomerWarehouseId.ToString(CultureInfo.InvariantCulture),30)+" "
            +Pad(request.DistrictId.ToString(CultureInfo.InvariantCulture),30)+" "
            +Pad(request.WarehouseId.ToString(CultureInfo.InvariantCulture),30)+" "
            +Pad(amount,8)+" "
            +Pad(date,30)+" "
            +new string(' ',11)+" "
            +new string(' ',11)+" ";
    }
    private static string Pad(string text,int length){
        if(text.Length>=length){
            return text[..length];
        }
        return text.PadRight(length);
    }
    private static string Truncate(string text){
        if(text.Length>CustomerDataLength){
            return text[..CustomerDataLength];
        }
        return text;
    }
}
