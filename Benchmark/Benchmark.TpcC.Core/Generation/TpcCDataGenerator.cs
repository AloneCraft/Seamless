using System.Text;
namespace Benchmark.TpcC.Core;
/// <summary>
/// 初期データの生成(TPC-C 5.11 の 4.3.3.1)。同じ (規模, 規則, 種) なら毎回同じデータを同じ順に作る。
/// 倉庫ごと・地区ごとに乱数の流れを分けるので、倉庫を足しても前の倉庫のデータは変わらない。
/// </summary>
public sealed class TpcCDataGenerator(TpcCScale scale,TpcCLoadRules rules,ulong seed){
    private static readonly string[] Syllables=["BAR","OUGHT","ABLE","PRI","PRES","ESE","ANTI","CALLY","ATION","EING"];
    private const string AlphaNumeric="abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public TpcCScale Scale{get;}=scale;
    public TpcCLoadRules Rules{get;}=rules;
    public ulong Seed{get;}=seed;
    /// <summary>C_LAST の NURand(255,0,999,C) の C(読み込み時)。</summary>
    public int LastNameCLoad{
        get{
            if(this.Rules.LastNameCLoad is int fixedValue){
                return fixedValue;
            }
            var rng=Rng.For(this.Seed,"c-load",0);
            return rng.Range(0,255);
        }
    }
    /// <summary>仕様 4.3.2.3 の姓(番号 0〜999 を 3 つの音節にする)。</summary>
    public static string LastName(int number){
        return Syllables[number/100]+Syllables[number/10%10]+Syllables[number%10];
    }
    public void Generate(ITpcCLoadSink sink){
        this.GenerateItems(sink);
        for(var warehouse=1;warehouse<=this.Scale.Warehouses;warehouse++){
            this.GenerateWarehouse(sink,warehouse);
            this.GenerateStock(sink,warehouse);
            for(var district=1;district<=TpcCScale.DistrictsPerWarehouse;district++){
                this.GenerateDistrict(sink,warehouse,district);
            }
        }
    }
    private void GenerateItems(ITpcCLoadSink sink){
        var rng=Rng.For(this.Seed,"item",0);
        for(var id=1;id<=this.Scale.Items;id++){
            var name=AString(ref rng,14,24);
            var price=rng.Range(100,10_000)/100m;
            var data=this.Pad(this.DataWithOriginal(ref rng),50);
            var image=rng.Range(1,10_000);
            sink.Load(new ItemRow(id,image,name,price,data));
        }
    }
    // 初期の W_YTD = 履歴の合計(3.3.2.8)、D_YTD = 地区の履歴の合計(3.3.2.9)になるよう、顧客 1 人あたり 10.00 で決める(仕様の規模では 300,000.00 と 30,000.00)。
    private void GenerateWarehouse(ITpcCLoadSink sink,int warehouse){
        var rng=Rng.For(this.Seed,"warehouse",warehouse);
        sink.Load(new WarehouseRow(warehouse,AString(ref rng,6,10),AString(ref rng,10,20),AString(ref rng,10,20),AString(ref rng,10,20),State(ref rng),Zip(ref rng),this.Tax(ref rng),this.Scale.CustomersPerDistrict*TpcCScale.DistrictsPerWarehouse*10.00m));
    }
    private void GenerateStock(ITpcCLoadSink sink,int warehouse){
        var rng=Rng.For(this.Seed,"stock",warehouse);
        for(var item=1;item<=this.Scale.Items;item++){
            var quantity=rng.Range(10,100);
            sink.Load(new StockRow(
                warehouse,item,quantity,
                AString(ref rng,24,24),AString(ref rng,24,24),AString(ref rng,24,24),AString(ref rng,24,24),AString(ref rng,24,24),
                AString(ref rng,24,24),AString(ref rng,24,24),AString(ref rng,24,24),AString(ref rng,24,24),AString(ref rng,24,24),
                0,0,0,this.Pad(this.DataWithOriginal(ref rng),50)));
        }
    }
    private void GenerateDistrict(ITpcCLoadSink sink,int warehouse,int district){
        var rng=Rng.For(this.Seed,"district",warehouse*100L+district);
        var customers=this.Scale.CustomersPerDistrict;
        sink.Load(new DistrictRow(warehouse,district,AString(ref rng,6,10),AString(ref rng,10,20),AString(ref rng,10,20),AString(ref rng,10,20),State(ref rng),Zip(ref rng),this.Tax(ref rng),customers*10.00m,customers+1,customers+1));
        var cLoad=this.LastNameCLoad;
        for(var customer=1;customer<=customers;customer++){
            var number=0;
            if(customer<=1000){
                number=customer-1;
            }else{
                number=(int)rng.NURand(255,0,999,cLoad);
            }
            var first=AString(ref rng,8,16);
            var credit="GC";
            if(rng.Percent(this.Rules.BadCreditPercent)){
                credit="BC";
            }
            sink.Load(new CustomerRow(
                warehouse,district,customer,first,"OE",LastName(number),AString(ref rng,10,20),AString(ref rng,10,20),AString(ref rng,10,20),State(ref rng),Zip(ref rng),NString(ref rng,16,16),
                this.Rules.LoadTime,credit,50_000.00m,this.Discount(ref rng),-10.00m,10.00m,1,0,this.Pad(AString(ref rng,300,500),500)));
            sink.Load(new HistoryRow(warehouse,district,customer,warehouse,district,customer,this.Rules.LoadTime,10.00m,AString(ref rng,12,24)));
        }
        // 初期の注文は顧客ごとに 1 件。O_C_ID は顧客 ID の無作為な順列。
        var permutation=new int[customers];
        for(var index=0;index<customers;index++){
            permutation[index]=index+1;
        }
        if(this.Rules.OrderCustomerIsPermutation){
            for(var index=customers-1;index>0;index--){
                var other=rng.Range(0,index);
                (permutation[index],permutation[other])=(permutation[other],permutation[index]);
            }
        }
        var firstNewOrder=this.Scale.FirstInitialNewOrder;
        for(var order=1;order<=this.Scale.InitialOrdersPerDistrict;order++){
            var delivered=order<firstNewOrder;
            var lines=rng.Range(5,15);
            var carrier=0;
            if(delivered){
                carrier=rng.Range(1,10);
            }
            var orderCustomer=permutation[order-1];
            if(!this.Rules.OrderCustomerIsPermutation){
                orderCustomer=rng.Range(order,customers);
            }
            sink.Load(new OrderRow(warehouse,district,order,orderCustomer,this.Rules.LoadTime,carrier,lines,1));
            if(!delivered){
                sink.Load(new NewOrderRow(warehouse,district,order));
            }
            for(var number=1;number<=lines;number++){
                var item=rng.Range(1,this.Scale.Items);
                var deliveryDate=0L;
                var amount=0m;
                if(delivered){
                    deliveryDate=this.Rules.LoadTime;
                    if(this.Rules.InvertedInitialLineAmounts){
                        amount=rng.Range(10,10_000)/100m;
                    }
                }else if(!this.Rules.InvertedInitialLineAmounts){
                    amount=rng.Range(1,999_999)/100m;
                }
                sink.Load(new OrderLineRow(warehouse,district,order,number,item,warehouse,deliveryDate,5,amount,AString(ref rng,24,24)));
            }
        }
    }
    /// <summary>固定長の列(HammerDB の char(n))なら空白で詰める。</summary>
    private string Pad(string text,int width){
        if(this.Rules.PadDataColumns){
            return text.PadRight(width);
        }
        return text;
    }
    private decimal Tax(ref Rng rng)=>rng.Range(this.Rules.TaxMin/this.Rules.TaxStep,this.Rules.TaxMax/this.Rules.TaxStep)*this.Rules.TaxStep/10_000m;
    private decimal Discount(ref Rng rng)=>rng.Range(0,5000/this.Rules.DiscountStep)*this.Rules.DiscountStep/10_000m;
    /// <summary>26〜50 文字の a-string。10% は無作為な位置に印(ORIGINAL)を埋める。</summary>
    private string DataWithOriginal(ref Rng rng){
        var text=AString(ref rng,26,50);
        if(!rng.Percent(10)){
            return text;
        }
        var marker=this.Rules.OriginalMarker;
        var start=rng.Range(0,text.Length-marker.Length);
        return string.Concat(text.AsSpan(0,start),marker,text.AsSpan(start+marker.Length));
    }
    private static string State(ref Rng rng){
        return string.Concat((char)('A'+rng.Range(0,25)),(char)('A'+rng.Range(0,25)));
    }
    /// <summary>n-string(4) + "11111"(4.3.2.7)。</summary>
    private static string Zip(ref Rng rng)=>NString(ref rng,4,4)+"11111";
    private static string AString(ref Rng rng,int min,int max){
        var length=rng.Range(min,max);
        var builder=new StringBuilder(length);
        for(var index=0;index<length;index++){
            builder.Append(AlphaNumeric[rng.Range(0,AlphaNumeric.Length-1)]);
        }
        return builder.ToString();
    }
    private static string NString(ref Rng rng,int min,int max){
        var length=rng.Range(min,max);
        var builder=new StringBuilder(length);
        for(var index=0;index<length;index++){
            builder.Append((char)('0'+rng.Range(0,9)));
        }
        return builder.ToString();
    }
}
