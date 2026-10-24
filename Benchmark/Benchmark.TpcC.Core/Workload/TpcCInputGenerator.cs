namespace Benchmark.TpcC.Core;
/// <summary>
/// NURand の定数 C。仕様 2.1.6.1: C_LAST の運転時の C は、作成時の C との差が 65〜119(96 と 112 を除く)になるように選ぶ。
/// HammerDB(Q-006)は作成時も運転時も 123 に固定する(差 0)。
/// </summary>
public sealed record TpcCNuRandConstants(int LastNameLoad,int LastNameRun,int CustomerId,int ItemId){
    public static TpcCNuRandConstants For(TpcCMode mode,ulong seed,int lastNameLoad){
        if(mode==TpcCMode.HammerDbMssql){
            // 顧客 ID と品目 ID は NURand を使わない(一様)ので、定数は使われない。
            return new TpcCNuRandConstants(123,123,0,0);
        }
        var rng=Rng.For(seed,"nurand-c",0);
        var delta=0;
        while(delta==0||delta==96||delta==112){
            delta=rng.Range(65,119);
        }
        var run=lastNameLoad+delta;
        if(run>255){
            run=lastNameLoad-delta;
        }
        return new TpcCNuRandConstants(lastNameLoad,run,rng.Range(0,1023),rng.Range(0,8191));
    }
}
/// <summary>
/// 取引の入力の生成(仕様 2.4.1〜2.8.1、5.2.4)。<see cref="TpcCMode.Spec"/> は仕様どおり(NURand で偏らせ、取引は 45/43/4/4/4 の重み付き選択)、
/// <see cref="TpcCMode.HammerDbMssql"/> は HammerDB 6.1 の運転側と手続きの中の乱数の分布(QUIRKS Q-001〜Q-009)を再現する
/// (取引は 1..23 の一様乱数、顧客と品目は一様、C_LAST だけ NURand(255,0,999,C=123)、遠隔の抽選、支払額は 1..5000 の整数)。
/// 端末(HammerDB の VU)ごとに生成器を 1 つ作る。要求番号から乱数を毎回作り直すので、同じ (種, 端末, 要求番号) なら同じ入力で、順序に依らない。
/// </summary>
public sealed class TpcCInputGenerator{
    private readonly TpcCScale scale;
    private readonly TpcCMode mode;
    private readonly ulong seed;
    private readonly int terminal;
    private readonly TpcCNuRandConstants constants;
    private readonly int[]? warehouseList;
    /// <param name="warehouseCount">HammerDB の「全倉庫を使う」を入れるときの VU の数。0 なら家の倉庫は端末ごとに 1 つに固定する。</param>
    public TpcCInputGenerator(TpcCScale scale,TpcCMode mode,ulong seed,int terminal,TpcCNuRandConstants constants,int allWarehousesTerminalCount=0){
        this.scale=scale;
        this.mode=mode;
        this.seed=seed;
        this.terminal=terminal;
        this.constants=constants;
        var setup=Rng.For(seed,"terminal-setup",terminal);
        if(mode==TpcCMode.HammerDbMssql){
            // VU ごとに、家の倉庫は 1..W から重複を許して一様に選ぶ(Q-002)。Stock-Level の地区は VU ごとに 1..10 から選んで固定する(Q-004)。
            this.HomeWarehouse=setup.Range(1,scale.Warehouses);
            this.StockLevelDistrict=setup.Range(1,TpcCScale.DistrictsPerWarehouse);
        }else{
            // 仕様: 端末は倉庫あたり 10 台で、倉庫 (端末/10)+1 に固定し、Stock-Level の地区は端末ごとに一意(端末 mod 10 + 1)。
            this.HomeWarehouse=terminal/TpcCScale.DistrictsPerWarehouse%scale.Warehouses+1;
            this.StockLevelDistrict=terminal%TpcCScale.DistrictsPerWarehouse+1;
        }
        if(allWarehousesTerminalCount>0){
            // 「全倉庫を使う」(Q-003): i 番目の VU は、((i + a×V) mod W)+1 を a=0..k-1 について受け持つ(k=ceil(W/V))。取引ごとに受け持ちから一様に選ぶ。
            var perTerminal=(scale.Warehouses+allWarehousesTerminalCount-1)/allWarehousesTerminalCount;
            var list=new int[perTerminal];
            for(var a=0;a<perTerminal;a++){
                list[a]=(terminal+a*allWarehousesTerminalCount)%scale.Warehouses+1;
            }
            this.warehouseList=list;
        }
    }
    public TpcCMode Mode=>this.mode;
    public int HomeWarehouse{get;}
    public int StockLevelDistrict{get;}
    /// <summary>要求番号 <paramref name="index"/> の入力。取引の種類も混合の規則で決める。</summary>
    public TpcCRequest Next(long index,long now){
        var rng=Rng.For(this.seed,"t"+this.terminal.ToString(System.Globalization.CultureInfo.InvariantCulture),index);
        return this.Make(ref rng,this.ChooseKind(ref rng),now);
    }
    /// <summary>種類を指定して要求番号 <paramref name="index"/> の入力を作る(試験と、種類ごとの検証用)。</summary>
    public TpcCRequest Next(long index,long now,TpcCTxnKind kind){
        var rng=Rng.For(this.seed,"t"+this.terminal.ToString(System.Globalization.CultureInfo.InvariantCulture),index);
        this.ChooseKind(ref rng);
        return this.Make(ref rng,kind,now);
    }
    private TpcCTxnKind ChooseKind(ref Rng rng){
        if(this.mode==TpcCMode.HammerDbMssql){
            // 1..23 の一様乱数: 1..10 New-Order、11..20 Payment、21 Delivery、22 Stock-Level、23 Order-Status(Q-001)。
            var draw=rng.Range(1,23);
            if(draw<=10){
                return TpcCTxnKind.NewOrder;
            }
            if(draw<=20){
                return TpcCTxnKind.Payment;
            }
            if(draw==21){
                return TpcCTxnKind.Delivery;
            }
            if(draw==22){
                return TpcCTxnKind.StockLevel;
            }
            return TpcCTxnKind.OrderStatus;
        }
        // 仕様 5.2.3: Payment 43%・Order-Status 4%・Delivery 4%・Stock-Level 4% が最低で、残りの 45% が New-Order(重み付き選択、5.2.4.1)。
        var choice=rng.Range(1,100);
        if(choice<=45){
            return TpcCTxnKind.NewOrder;
        }
        if(choice<=88){
            return TpcCTxnKind.Payment;
        }
        if(choice<=92){
            return TpcCTxnKind.OrderStatus;
        }
        if(choice<=96){
            return TpcCTxnKind.Delivery;
        }
        return TpcCTxnKind.StockLevel;
    }
    private TpcCRequest Make(ref Rng rng,TpcCTxnKind kind,long now){
        var home=this.HomeWarehouse;
        if(this.warehouseList is not null){
            home=this.warehouseList[rng.Range(0,this.warehouseList.Length-1)];
        }
        switch(kind){
            case TpcCTxnKind.NewOrder:
                return this.NewOrder(ref rng,home,now);
            case TpcCTxnKind.Payment:
                return this.Payment(ref rng,home,now);
            case TpcCTxnKind.OrderStatus:
                return this.OrderStatus(ref rng,home,now);
            case TpcCTxnKind.Delivery:
                return new DeliveryRequest(home,now,rng.Range(1,10));
            default:
                return new StockLevelRequest(home,now,this.StockLevelDistrict,rng.Range(10,20));
        }
    }
    private int District(ref Rng rng)=>rng.Range(1,TpcCScale.DistrictsPerWarehouse);
    private int CustomerId(ref Rng rng){
        if(this.mode==TpcCMode.HammerDbMssql){
            return rng.Range(1,this.scale.CustomersPerDistrict);
        }
        return (int)rng.NURand(this.scale.CustomerIdA,1,this.scale.CustomersPerDistrict,this.constants.CustomerId);
    }
    private int ItemId(ref Rng rng){
        if(this.mode==TpcCMode.HammerDbMssql){
            return rng.Range(1,this.scale.Items);
        }
        return (int)rng.NURand(this.scale.ItemIdA,1,this.scale.Items,this.constants.ItemId);
    }
    private string LastName(ref Rng rng){
        var number=(int)rng.NURand(255,0,999,this.constants.LastNameRun);
        return TpcCDataGenerator.LastName(number);
    }
    /// <summary>家の倉庫以外の倉庫を一様に選ぶ。倉庫が 1 つしか無いときは家の倉庫のまま。</summary>
    private int OtherWarehouse(ref Rng rng,int home){
        if(this.scale.Warehouses<2){
            return home;
        }
        var other=rng.Range(1,this.scale.Warehouses-1);
        if(other>=home){
            other++;
        }
        return other;
    }
    private NewOrderRequest NewOrder(ref Rng rng,int home,long now){
        var district=this.District(ref rng);
        var customer=this.CustomerId(ref rng);
        var count=rng.Range(5,15);
        var rollback=rng.Range(1,100)==1;
        var allLocal=1;
        var lines=new NewOrderLine[count];
        for(var number=0;number<count;number++){
            var item=this.ItemId(ref rng);
            if(rollback&&number==count-1){
                // 巻き戻しの印: 最後の明細は存在しない品目(未使用の品目番号)にする。
                item=this.scale.Items+1;
            }
            var supply=home;
            if(rng.Range(1,100)==1){
                // 1% の明細は遠隔の倉庫から出す。HammerDB は W=1 でも抽選に当たれば O_ALL_LOCAL を 0 にする(Q-029)。
                if(this.mode==TpcCMode.HammerDbMssql){
                    allLocal=0;
                    supply=this.OtherWarehouse(ref rng,home);
                }else if(this.scale.Warehouses>1){
                    supply=this.OtherWarehouse(ref rng,home);
                }
            }
            var quantity=rng.Range(1,10);
            if(supply!=home){
                allLocal=0;
            }
            lines[number]=new NewOrderLine(item,supply,quantity);
        }
        return new NewOrderRequest(home,now,district,customer,lines,allLocal);
    }
    private PaymentRequest Payment(ref Rng rng,int home,long now){
        var district=this.District(ref rng);
        var customerWarehouse=home;
        var customerDistrict=district;
        if(rng.Range(1,100)>85){
            // 15% は遠隔の顧客: 顧客の地区は 1..10 から選び直し、倉庫は家以外から一様(W=1 では家のまま、Q-009)。
            customerDistrict=this.District(ref rng);
            customerWarehouse=this.OtherWarehouse(ref rng,home);
        }
        // 名前と番号は、どちらで引くかにかかわらず毎回両方の乱数を引く(HammerDB の運転側と同じ)。
        var byName=rng.Range(1,100)<=60;
        var last=this.LastName(ref rng);
        var customer=this.CustomerId(ref rng);
        decimal amount;
        if(this.mode==TpcCMode.HammerDbMssql){
            // 支払額は 1..5000 の整数(ドル単位、Q-008)。
            amount=rng.Range(1,5000);
        }else{
            // 仕様 2.5.1.3: 1.00〜5,000.00(セント単位)。
            amount=rng.Range(100,500_000)/100m;
        }
        string? name=null;
        if(byName){
            name=last;
        }
        return new PaymentRequest(home,now,district,customerWarehouse,customerDistrict,customer,name,amount);
    }
    private OrderStatusRequest OrderStatus(ref Rng rng,int home,long now){
        var district=this.District(ref rng);
        var byName=rng.Range(1,100)<=60;
        var last=this.LastName(ref rng);
        var customer=this.CustomerId(ref rng);
        string? name=null;
        if(byName){
            name=last;
        }
        return new OrderStatusRequest(home,now,district,customer,name);
    }
}
