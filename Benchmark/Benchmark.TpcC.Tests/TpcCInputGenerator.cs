namespace Benchmark.TpcC.Tests;
public sealed class TpcCInputGenerator{
    private static Core.TpcCInputGenerator Make(Core.TpcCScale scale,Core.TpcCMode mode,int terminal=0,ulong seed=1,int allWarehouses=0){
        var load=new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(mode),seed).LastNameCLoad;
        return new Core.TpcCInputGenerator(scale,mode,seed,terminal,Core.TpcCNuRandConstants.For(scale,mode,seed,load),allWarehouses);
    }
    private sealed class Mix{
        public readonly int[] Counts=new int[5];
        public int NewOrders;
        public int Rollbacks;
        public long Lines;
        public long RemoteLines;
        public int NotAllLocal;
        public int Payments;
        public int RemotePayments;
        public int PaymentsByName;
        public int OrderStatuses;
        public int OrderStatusesByName;
    }
    private static Mix Sample(Core.TpcCInputGenerator generator,Core.TpcCScale scale,int count){
        var mix=new Mix();
        for(long index=0;index<count;index++){
            var request=generator.Next(index,0);
            mix.Counts[(int)request.Kind]++;
            switch(request){
                case Core.NewOrderRequest order:
                    mix.NewOrders++;
                    if(order.Lines.Any(line=>line.ItemId>scale.Items)){
                        mix.Rollbacks++;
                    }
                    mix.Lines+=order.Lines.Length;
                    mix.RemoteLines+=order.Lines.Count(line=>line.SupplyWarehouseId!=order.WarehouseId);
                    if(order.AllLocal==0){
                        mix.NotAllLocal++;
                    }
                    break;
                case Core.PaymentRequest payment:
                    mix.Payments++;
                    if(payment.CustomerWarehouseId!=payment.WarehouseId){
                        mix.RemotePayments++;
                    }
                    if(payment.CustomerLast is not null){
                        mix.PaymentsByName++;
                    }
                    break;
                case Core.OrderStatusRequest status:
                    mix.OrderStatuses++;
                    if(status.CustomerLast is not null){
                        mix.OrderStatusesByName++;
                    }
                    break;
            }
        }
        return mix;
    }
    private static double Share(int part,int total)=>part/(double)total;
    [Fact,Trait("Category","Unit")]public void Next(){
        {
            //同じ (種, 端末, 要求番号) なら同じ入力、違う端末なら違う入力
            var scale=TestDatabases.Mini(4,2);
            var first=Make(scale,Core.TpcCMode.Spec);
            var again=Make(scale,Core.TpcCMode.Spec);
            var other=Make(scale,Core.TpcCMode.Spec,terminal:11);
            var different=0;
            for(long index=0;index<50;index++){
                Assert.Equal(CanonicalWriter.ToBytes(first.Next(index,5)),CanonicalWriter.ToBytes(again.Next(index,5)));
                if(!CanonicalWriter.ToBytes(first.Next(index,5)).AsSpan().SequenceEqual(CanonicalWriter.ToBytes(other.Next(index,5)))){
                    different++;
                }
            }
            Assert.True(different>40);
        }
        {
            //仕様の形の混合は 5.2.4.2 の山札(23 枚: New-Order 10・Payment 10・他 1 枚ずつ)。最低比率(5.2.3: Payment 43%・他 4%)を割らない。
            //巻き戻し約 1%、明細は平均 10 行で遠隔 1%、遠隔の Payment 15%、姓で引く割合 60%(5.5.1.5 の帯)
            var scale=new Core.TpcCScale{Warehouses=10};
            var mix=Sample(Make(scale,Core.TpcCMode.Spec,terminal:3),scale,40_000);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.NewOrder],40_000),10/23.0-0.001,10/23.0+0.001);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.Payment],40_000),0.43,10/23.0+0.001);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.OrderStatus],40_000),0.04,1/23.0+0.001);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.Delivery],40_000),0.04,1/23.0+0.001);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.StockLevel],40_000),0.04,1/23.0+0.001);
            Assert.InRange(Share(mix.Rollbacks,mix.NewOrders),0.008,0.012);
            Assert.InRange(mix.Lines/(double)mix.NewOrders,9.9,10.1);
            Assert.InRange(mix.RemoteLines/(double)mix.Lines,0.009,0.011);
            Assert.InRange(Share(mix.RemotePayments,mix.Payments),0.142,0.158);
            Assert.InRange(Share(mix.PaymentsByName,mix.Payments),0.585,0.615);
            Assert.InRange(Share(mix.OrderStatusesByName,mix.OrderStatuses),0.55,0.65);
        }
        {
            //HammerDB の形の混合(Q-001): 1..23 の一様乱数で New-Order と Payment が 10/23、他が 1/23 ずつ
            var scale=new Core.TpcCScale{Warehouses=10};
            var mix=Sample(Make(scale,Core.TpcCMode.HammerDbMssql,terminal:3),scale,40_000);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.NewOrder],40_000),10/23.0-0.015,10/23.0+0.015);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.Payment],40_000),10/23.0-0.015,10/23.0+0.015);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.OrderStatus],40_000),1/23.0-0.007,1/23.0+0.007);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.Delivery],40_000),1/23.0-0.007,1/23.0+0.007);
            Assert.InRange(Share(mix.Counts[(int)Core.TpcCTxnKind.StockLevel],40_000),1/23.0-0.007,1/23.0+0.007);
            Assert.InRange(Share(mix.Rollbacks,mix.NewOrders),0.008,0.012);
            Assert.InRange(mix.RemoteLines/(double)mix.Lines,0.009,0.011);
            Assert.InRange(Share(mix.RemotePayments,mix.Payments),0.142,0.158);
            Assert.InRange(Share(mix.PaymentsByName,mix.Payments),0.585,0.615);
        }
    }
    [Fact,Trait("Category","Unit")]public void Deck(){
        {
            //仕様の形: 23 件ごと(山札 1 周)にちょうど New-Order 10・Payment 10・Order-Status/Delivery/Stock-Level 1 ずつ。どの区切りでも最低比率を割らない
            var scale=new Core.TpcCScale{Warehouses=2};
            var generator=Make(scale,Core.TpcCMode.Spec,terminal:5);
            for(long deck=0;deck<50;deck++){
                var counts=new int[5];
                for(long position=0;position<23;position++){
                    counts[(int)generator.Next(deck*23+position,0).Kind]++;
                }
                Assert.Equal(new[]{10,10,1,1,1},new[]{counts[(int)Core.TpcCTxnKind.NewOrder],counts[(int)Core.TpcCTxnKind.Payment],counts[(int)Core.TpcCTxnKind.OrderStatus],counts[(int)Core.TpcCTxnKind.Delivery],counts[(int)Core.TpcCTxnKind.StockLevel]});
            }
            //順序は周ごとに変わり、呼ぶ順序に依らない(後ろの番号を先に呼んでも同じ)
            var forward=Enumerable.Range(0,46).Select(index=>generator.Next(index,0).Kind).ToArray();
            Assert.NotEqual(forward[..23],forward[23..]);
            var other=Make(scale,Core.TpcCMode.Spec,terminal:5);
            var backward=Enumerable.Range(0,46).Reverse().Select(index=>other.Next(index,0).Kind).Reverse().ToArray();
            Assert.Equal(forward,backward);
        }
    }
    [Fact,Trait("Category","Unit")]public void Constructor(){
        {
            //仕様の形は端末が倉庫あたり 10 台の範囲を超えると受け付けず、「全倉庫を使う」(HammerDB の設定)も受け付けない
            var scale=new Core.TpcCScale{Warehouses=3};
            var constants=Core.TpcCNuRandConstants.For(scale,Core.TpcCMode.Spec,1,5);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new Core.TpcCInputGenerator(scale,Core.TpcCMode.Spec,1,30,constants));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new Core.TpcCInputGenerator(scale,Core.TpcCMode.Spec,1,-1,constants));
            Assert.Throws<ArgumentException>(()=>new Core.TpcCInputGenerator(scale,Core.TpcCMode.Spec,1,0,constants,3));
            _=new Core.TpcCInputGenerator(scale,Core.TpcCMode.Spec,1,29,constants);
            _=new Core.TpcCInputGenerator(scale,Core.TpcCMode.HammerDbMssql,1,500,Core.TpcCNuRandConstants.For(scale,Core.TpcCMode.HammerDbMssql,1,123),3);
        }
    }
    [Fact,Trait("Category","Unit")]public void ReducedScale(){
        {
            //縮小した規模(地区あたり顧客 30): NURand の C は [0..A] に収まり、姓の番号は存在する 0..29 から引くので、姓で引く入力は必ず該当者がいる
            var scale=TestDatabases.Mini(2,1);
            Assert.Equal(30,scale.LastNameCount);
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                var load=new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(mode),1).LastNameCLoad;
                var constants=Core.TpcCNuRandConstants.For(scale,mode,9,load);
                if(mode==Core.TpcCMode.Spec){
                    Assert.InRange(constants.CustomerId,0,scale.CustomerIdA);
                    Assert.InRange(constants.ItemId,0,scale.ItemIdA);
                    Assert.InRange(constants.LastNameRun,0,scale.LastNameA);
                }
                var generator=Make(scale,mode,terminal:2,seed:9);
                var existing=new HashSet<string>(Enumerable.Range(0,30).Select(Core.TpcCDataGenerator.LastName));
                var named=0;
                for(long index=0;index<3_000;index++){
                    var request=generator.Next(index,0);
                    string? last=null;
                    if(request is Core.PaymentRequest payment){
                        last=payment.CustomerLast;
                    }else if(request is Core.OrderStatusRequest status){
                        last=status.CustomerLast;
                    }
                    if(last is not null){
                        named++;
                        Assert.Contains(last,existing);
                    }
                }
                Assert.True(named>200,$"{mode}: {named}");
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void Distributions(){
        {
            //仕様の形の顧客 ID は NURand で偏り、HammerDB の形は一様(Q-005): 最も多く出た 300 個の顧客 ID が占める割合
            var scale=new Core.TpcCScale{Warehouses=1};
            var share=new Dictionary<Core.TpcCMode,double>();
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                var generator=Make(scale,mode);
                var counts=new int[scale.CustomersPerDistrict+1];
                const int Samples=200_000;
                for(long index=0;index<Samples;index++){
                    counts[((Core.NewOrderRequest)generator.Next(index,0,Core.TpcCTxnKind.NewOrder)).CustomerId]++;
                }
                share[mode]=counts.OrderByDescending(count=>count).Take(300).Sum()/(double)Samples;
            }
            Assert.True(share[Core.TpcCMode.HammerDbMssql]<0.13,$"uniform top share {share[Core.TpcCMode.HammerDbMssql]}");
            Assert.True(share[Core.TpcCMode.Spec]>0.16,$"NURand top share {share[Core.TpcCMode.Spec]}");
        }
        {
            //品目 ID も同じ: 仕様の形は NURand(8191) で偏り、HammerDB の形は一様。ばらつきの小さい指標として、出現した品目の種類の割合を比べる
            var scale=new Core.TpcCScale{Warehouses=1};
            var distinct=new Dictionary<Core.TpcCMode,double>();
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                var generator=Make(scale,mode);
                var seen=new HashSet<int>();
                for(long index=0;index<50_000;index++){
                    foreach(var line in ((Core.NewOrderRequest)generator.Next(index,0,Core.TpcCTxnKind.NewOrder)).Lines){
                        if(line.ItemId<=scale.Items){
                            seen.Add(line.ItemId);
                        }
                    }
                }
                distinct[mode]=seen.Count/(double)scale.Items;
            }
            Assert.True(distinct[Core.TpcCMode.HammerDbMssql]>0.98,$"uniform distinct share {distinct[Core.TpcCMode.HammerDbMssql]}");
            Assert.True(distinct[Core.TpcCMode.Spec]<0.95,$"NURand distinct share {distinct[Core.TpcCMode.Spec]}");
        }
        {
            //支払額: 仕様は 1.00〜5,000.00 のセント単位、HammerDB は 1..5000 の整数(Q-008)
            var scale=new Core.TpcCScale{Warehouses=2};
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                var generator=Make(scale,mode);
                var amounts=new List<decimal>();
                for(long index=0;index<2_000;index++){
                    amounts.Add(((Core.PaymentRequest)generator.Next(index,0,Core.TpcCTxnKind.Payment)).Amount);
                }
                Assert.All(amounts,amount=>Assert.InRange(amount,1m,5000m));
                if(mode==Core.TpcCMode.HammerDbMssql){
                    Assert.All(amounts,amount=>Assert.Equal(decimal.Truncate(amount),amount));
                }else{
                    Assert.Contains(amounts,amount=>amount!=decimal.Truncate(amount));
                }
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void HomeWarehouse(){
        {
            //HammerDB の形: 家の倉庫は端末ごとに重複を許して 1..W から選んで固定、Stock-Level の地区も端末ごとに固定(Q-002・Q-004)
            var scale=new Core.TpcCScale{Warehouses=6};
            var homes=new HashSet<int>();
            for(var terminal=0;terminal<40;terminal++){
                var generator=Make(scale,Core.TpcCMode.HammerDbMssql,terminal);
                Assert.InRange(generator.HomeWarehouse,1,6);
                Assert.InRange(generator.StockLevelDistrict,1,10);
                homes.Add(generator.HomeWarehouse);
                for(long index=0;index<20;index++){
                    var request=generator.Next(index,0);
                    Assert.Equal(generator.HomeWarehouse,request.WarehouseId);
                    if(request is Core.StockLevelRequest stock){
                        Assert.Equal(generator.StockLevelDistrict,stock.DistrictId);
                    }
                }
            }
            Assert.True(homes.Count>=4);
        }
        {
            //仕様の形: 端末は倉庫あたり 10 台、倉庫 (端末/10)+1 に固定。Stock-Level の地区は端末ごとに一意(2.8.1.1)
            var scale=new Core.TpcCScale{Warehouses=3};
            var pairs=new HashSet<(int,int)>();
            for(var terminal=0;terminal<30;terminal++){
                var generator=Make(scale,Core.TpcCMode.Spec,terminal);
                Assert.Equal(terminal/10+1,generator.HomeWarehouse);
                pairs.Add((generator.HomeWarehouse,generator.StockLevelDistrict));
            }
            Assert.Equal(30,pairs.Count);
        }
        {
            //「全倉庫を使う」(Q-003): W=30・VU 3 人なら、1 人目は 1,4,7,…,28 を取引ごとに一様に選ぶ(HammerDB 文書 ch04s08 の例)
            var scale=new Core.TpcCScale{Warehouses=30};
            var generator=Make(scale,Core.TpcCMode.HammerDbMssql,terminal:0,allWarehouses:3);
            var seen=new SortedSet<int>();
            for(long index=0;index<3_000;index++){
                seen.Add(generator.Next(index,0,Core.TpcCTxnKind.Delivery).WarehouseId);
            }
            Assert.Equal(Enumerable.Range(0,10).Select(a=>a*3+1),seen);
        }
    }
    [Fact,Trait("Category","Unit")]public void SingleWarehouse(){
        {
            //W=1 の HammerDB の形: 遠隔の明細は家のまま、それでも約 9.5% の注文の O_ALL_LOCAL が 0(Q-029)。遠隔の Payment も顧客の倉庫は家のまま(Q-009)
            var scale=new Core.TpcCScale{Warehouses=1};
            var generator=Make(scale,Core.TpcCMode.HammerDbMssql);
            var notAllLocal=0;
            const int Orders=20_000;
            for(long index=0;index<Orders;index++){
                var order=(Core.NewOrderRequest)generator.Next(index,0,Core.TpcCTxnKind.NewOrder);
                Assert.All(order.Lines,line=>Assert.Equal(1,line.SupplyWarehouseId));
                if(order.AllLocal==0){
                    notAllLocal++;
                }
                var payment=(Core.PaymentRequest)generator.Next(index,0,Core.TpcCTxnKind.Payment);
                Assert.Equal(1,payment.CustomerWarehouseId);
            }
            Assert.InRange(notAllLocal/(double)Orders,0.085,0.105);
        }
        {
            //W=1 の仕様の形: 遠隔の明細は無く、O_ALL_LOCAL は常に 1
            var scale=new Core.TpcCScale{Warehouses=1};
            var generator=Make(scale,Core.TpcCMode.Spec);
            for(long index=0;index<5_000;index++){
                Assert.Equal(1,((Core.NewOrderRequest)generator.Next(index,0,Core.TpcCTxnKind.NewOrder)).AllLocal);
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void TouchedWarehouses(){
        {
            //触る倉庫は家の倉庫を含み、昇順・重複なし
            var scale=new Core.TpcCScale{Warehouses=8};
            var generator=Make(scale,Core.TpcCMode.Spec,terminal:12);
            for(long index=0;index<3_000;index++){
                var touched=generator.Next(index,0).TouchedWarehouses();
                Assert.Contains(generator.HomeWarehouse,touched);
                Assert.Equal(touched.Distinct().OrderBy(warehouse=>warehouse).ToArray(),touched);
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void NuRandConstants(){
        {
            //仕様の形: C_LAST の運転時の C は作成時の C と 65〜119(96 と 112 を除く)離れる。HammerDB の形は 123 固定で差が 0(Q-006)
            for(ulong seed=1;seed<=200;seed++){
                var load=new Core.TpcCDataGenerator(new Core.TpcCScale(),Core.TpcCLoadRules.For(Core.TpcCMode.Spec),seed).LastNameCLoad;
                var constants=Core.TpcCNuRandConstants.For(new Core.TpcCScale(),Core.TpcCMode.Spec,seed,load);
                var delta=Math.Abs(constants.LastNameRun-constants.LastNameLoad);
                Assert.InRange(delta,65,119);
                Assert.NotEqual(96,delta);
                Assert.NotEqual(112,delta);
                Assert.InRange(constants.LastNameRun,0,255);
            }
            var hammerdb=Core.TpcCNuRandConstants.For(new Core.TpcCScale(),Core.TpcCMode.HammerDbMssql,1,123);
            Assert.Equal(123,hammerdb.LastNameLoad);
            Assert.Equal(123,hammerdb.LastNameRun);
        }
    }
}
