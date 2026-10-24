using System.Diagnostics;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Spike;
/// <summary>区画の規模。倉庫ごとに地区 10、地区ごとに顧客と初期の注文を持つ。品目は区画ごとに複製する。</summary>
public sealed record Scale(int Warehouses,int Customers,int Items,int InitialOrders){
    public static Scale Tiny{get;}=new(1,60,500,60);
    public static Scale Small{get;}=new(1,300,5000,300);
    /// <summary>行数 約 110 万(倉庫 2、品目 10 万、顧客 3,000/地区)。</summary>
    public static Scale Million{get;}=new(2,3000,100_000,3000);
    public static Scale Parse(string text){
        return text switch{
            "tiny"=>Tiny,
            "small"=>Small,
            "million"=>Million,
            _=>throw new ArgumentException("unknown scale "+text),
        };
    }
}
public sealed record PartitionOptions{
    public bool Mirror{get;init;}=true;
    /// <summary>ログのメモリを小さくして、読み取りの多くをディスクへ行かせる(待ちの読み取りを起こす)。</summary>
    public bool SmallMemory{get;init;}
    public long MemoryBytes{get;init;}=512L*1024*1024;
    public int Seed{get;init;}=1;
}
public sealed record SaveRecord(Guid Token,long Serial,Digest[]? Expected,TimeSpan Elapsed);
/// <summary>
/// 区画 1 つ = 共通ストア 1 つ。表 9 個(Set)と索引 9 個(辞書、うち 1 個は保存時の要約)を持つ。
/// 取引は TPC-C を模した形(新規注文・支払い・配送・注文状況・在庫水準)で、区画の中だけで完結する。
/// </summary>
public sealed class Partition:IDisposable{
    public const int Districts=10;
    private readonly List<IPart> parts=[];
    private readonly Random rng;
    private long historySeq;
    private long serial;
    public Partition(int id,string directory,Scale scale,PartitionOptions options){
        this.Id=id;
        this.Directory=directory;
        this.Scale=scale;
        this.Options=options;
        this.rng=new(options.Seed*7919+id);
        var opened=Stopwatch.StartNew();
        this.Store=Store.Open(directory,new StoreLimits{MemoryBytes=options.MemoryBytes,DiskBytes=64L*1024*1024*1024});
        this.Recovered=this.Store.CurrentCheckpoint is not null;
        var w=(long)scale.Warehouses;
        // 実行中に増える注文の見込み(初期の 2 割+地区あたり 1,000)。索引の初期の大きさだけに効く(足りなければ自動で広がる)。
        var orders=w*Districts*(scale.InitialOrders+scale.InitialOrders/5+1000L);
        this.Warehouse=this.Table<Pad64>("warehouse",w);
        this.District=this.Table<Pad64>("district",w*Districts);
        this.Customer=this.Table<Pad600>("customer",w*Districts*scale.Customers);
        this.History=this.Table<Pad16>("history",w*Districts*scale.Customers+20000);
        this.NewOrder=this.Table<Pad8>("new_order",w*Districts*scale.InitialOrders);
        this.Order=this.Table<Pad8>("orders",orders);
        this.OrderLine=this.Table<Pad24>("order_line",orders*10);
        this.Item=this.Table<Pad56>("item",scale.Items);
        this.Stock=this.Table<Pad280>("stock",w*scale.Items);
        this.CustomerByLast=this.Index("x_customer_last",w*Districts*100);
        this.CustomerLastOrder=this.Index("x_customer_last_order",w*Districts*scale.Customers);
        this.NewOrderMin=this.Index("x_new_order_min",w*Districts);
        this.OrderLineCount=this.Index("x_order_line_count",orders);
        this.StockLow=this.Index("x_stock_low",w*scale.Items/4);
        this.HistorySeq=this.Index("x_history_seq",1);
        this.OrderCustomer=this.Index("x_order_customer",orders);
        this.PaymentByCustomer=this.Index("x_payment_by_customer",w*Districts*scale.Customers);
        this.Meta=new IndexStore(this.Store,"meta",this.Tuning(),64,withMirror:false);
        this.OpenElapsed=opened.Elapsed;
        if(this.Recovered){
            var rebuilt=Stopwatch.StartNew();
            foreach(var part in this.parts){
                part.RebuildMirror();
            }
            if(this.HistorySeq.TryGet(new K4(0,0,0,0),out var seq)){
                this.historySeq=seq.A;
            }
            if(this.Meta.TryGet(new K4(0,0,0,0),out var meta)){
                this.serial=meta.A;
            }
            this.MirrorElapsed=rebuilt.Elapsed;
        }
    }
    public int Id{get;}
    public string Directory{get;}
    public Scale Scale{get;}
    public PartitionOptions Options{get;}
    public Store Store{get;}
    public bool Recovered{get;}
    public TimeSpan OpenElapsed{get;}
    public TimeSpan MirrorElapsed{get;}
    /// <summary>区画を押さえる関所(所有者の切り替えに使う)。</summary>
    public SemaphoreSlim Gate{get;}=new(1,1);
    public long Transactions{get;private set;}
    public long Serial=>this.serial;
    public IReadOnlyList<IPart> Parts=>this.parts;
    public TableStore<Pad64> Warehouse{get;}
    public TableStore<Pad64> District{get;}
    public TableStore<Pad600> Customer{get;}
    public TableStore<Pad16> History{get;}
    public TableStore<Pad8> NewOrder{get;}
    public TableStore<Pad8> Order{get;}
    public TableStore<Pad24> OrderLine{get;}
    public TableStore<Pad56> Item{get;}
    public TableStore<Pad280> Stock{get;}
    public IndexStore CustomerByLast{get;}
    public IndexStore CustomerLastOrder{get;}
    public IndexStore NewOrderMin{get;}
    public IndexStore OrderLineCount{get;}
    public IndexStore StockLow{get;}
    public IndexStore HistorySeq{get;}
    public IndexStore OrderCustomer{get;}
    public IndexStore PaymentByCustomer{get;}
    public IndexStore Meta{get;}
    public long FirstWarehouse=>(long)this.Id*this.Scale.Warehouses+1;
    private TsavoriteTuning Tuning(){
        if(this.Options.SmallMemory){
            return TsavoriteTuning.Default with{PageSize=4096,LogMemorySize=4*4096};
        }
        return TsavoriteTuning.Default;
    }
    private TableStore<TPad> Table<TPad>(string name,long expected) where TPad:unmanaged{
        var table=new TableStore<TPad>(this.Store,name,this.Tuning(),expected,this.Options.Mirror);
        this.parts.Add(table);
        return table;
    }
    private IndexStore Index(string name,long expected){
        var index=new IndexStore(this.Store,name,this.Tuning(),expected,this.Options.Mirror);
        this.parts.Add(index);
        return index;
    }
    private static Row<TPad> NewRow<TPad>(K4 key,long x,long y,long fill) where TPad:unmanaged{
        var row=new Row<TPad>{K=key,X=x,Y=y};
        Hashing.Fill(ref row.Pad,fill);
        return row;
    }
    /// <summary>初期データを入れる(新規の区画だけ)。</summary>
    public void Load(){
        if(this.Recovered){
            throw new InvalidOperationException("The partition was recovered.");
        }
        var s=this.Scale;
        for(long i=1;i<=s.Items;i++){
            this.Item.Insert(NewRow<Pad56>(new(i,0,0,0),i*7,0,i));
        }
        for(var wi=0;wi<s.Warehouses;wi++){
            var w=this.FirstWarehouse+wi;
            this.Warehouse.Insert(NewRow<Pad64>(new(w,0,0,0),0,0,w));
            for(long i=1;i<=s.Items;i++){
                var qty=10+this.rng.Next(91);
                this.Stock.Insert(NewRow<Pad280>(new(w,i,0,0),qty,0,w*1_000_003+i));
            }
            for(long d=1;d<=Districts;d++){
                this.District.Insert(NewRow<Pad64>(new(w,d,0,0),s.InitialOrders+1,0,w*100+d));
                for(long c=1;c<=s.Customers;c++){
                    this.Customer.Insert(NewRow<Pad600>(new(w,d,c,0),0,0,(w*100+d)*100_000+c));
                    var last=new K4(w,d,c%100,0);
                    if(this.CustomerByLast.TryGet(last,out var entry)){
                        entry.A++;
                        this.CustomerByLast.Put(last,entry);
                    }else{
                        this.CustomerByLast.Put(last,new V24{A=1,B=c});
                    }
                }
                var firstNew=s.InitialOrders*7/10+1;
                for(long o=1;o<=s.InitialOrders;o++){
                    var c=1+(o*7919)%s.Customers;
                    var lines=5+this.rng.Next(11);
                    this.Order.Insert(NewRow<Pad8>(new(w,d,o,0),c,lines,o));
                    for(long n=1;n<=lines;n++){
                        this.OrderLine.Insert(NewRow<Pad24>(new(w,d,o,n),1+this.rng.Next(s.Items),5,o*16+n));
                    }
                    this.CustomerLastOrder.Put(new(w,d,c,0),new V24{A=o});
                    this.OrderLineCount.Put(new(w,d,o,0),new V24{A=lines});
                    this.OrderCustomer.Put(new(w,d,o,0),new V24{A=c});
                    if(o>=firstNew){
                        this.NewOrder.Insert(NewRow<Pad8>(new(w,d,o,0),0,0,o));
                    }
                }
                if(firstNew<=s.InitialOrders){
                    this.NewOrderMin.Put(new(w,d,0,0),new V24{A=firstNew});
                }
            }
        }
    }
    /// <summary>取引を 1 つ実行する。比率は新規注文 45・支払い 43・配送 4・注文状況 4・在庫水準 4。</summary>
    public void RunTransaction(){
        var pick=this.rng.Next(100);
        var w=this.FirstWarehouse+this.rng.Next(this.Scale.Warehouses);
        if(pick<45){
            this.NewOrderTxn(w);
        }else if(pick<88){
            this.Payment(w);
        }else if(pick<92){
            this.Delivery(w);
        }else if(pick<96){
            this.OrderStatus(w);
        }else{
            this.StockLevel(w);
        }
        this.Transactions++;
    }
    /// <summary>1〜3 個の操作だけの小さな単位(所有者の受け渡しの試しで使う)。</summary>
    public void RunSmallUnit(){
        var w=this.FirstWarehouse+this.rng.Next(this.Scale.Warehouses);
        var d=1+this.rng.Next(Districts);
        var kind=this.rng.Next(6);
        if(kind==0){
            var c=1+this.rng.Next(this.Scale.Customers);
            var row=this.Customer.Get(new(w,d,c,0));
            row.X+=1;
            this.Customer.Replace(row);
        }else if(kind==1){
            var i=1+this.rng.Next(this.Scale.Items);
            var row=this.Stock.Get(new(w,i,0,0));
            row.Y+=1;
            this.Stock.Replace(row);
            if(row.Y%3==0){
                this.StockLow.Put(new(w,i,0,0),new V24{A=row.Y});
            }else{
                this.StockLow.Remove(new(w,i,0,0));
            }
        }else if(kind==2){
            this.historySeq++;
            this.History.Insert(NewRow<Pad16>(new(this.Id,this.historySeq,0,0),w,d,this.historySeq));
            this.HistorySeq.Put(new(0,0,0,0),new V24{A=this.historySeq});
        }else if(kind==3){
            this.Item.Get(new(1+this.rng.Next(this.Scale.Items),0,0,0));
        }else if(kind==4){
            var c=1+this.rng.Next(this.Scale.Customers);
            this.CustomerLastOrder.TryGet(new(w,d,c,0),out _);
        }else{
            var key=new K4(w,d,this.rng.Next(50),99);
            if(this.PaymentByCustomer.TryGet(key,out _)){
                this.PaymentByCustomer.Remove(key);
            }else{
                this.PaymentByCustomer.Put(key,new V24{A=1});
            }
        }
        this.Transactions++;
    }
    private void NewOrderTxn(long w){
        var d=1+this.rng.Next(Districts);
        var c=1+this.rng.Next(this.Scale.Customers);
        this.Warehouse.Get(new(w,0,0,0));
        var district=this.District.Get(new(w,d,0,0));
        var o=district.X;
        district.X++;
        this.District.Replace(district);
        this.Customer.Get(new(w,d,c,0));
        var lines=5+this.rng.Next(11);
        this.Order.Insert(NewRow<Pad8>(new(w,d,o,0),c,lines,o));
        this.NewOrder.Insert(NewRow<Pad8>(new(w,d,o,0),0,0,o));
        for(long n=1;n<=lines;n++){
            var i=1+this.rng.Next(this.Scale.Items);
            this.Item.Get(new(i,0,0,0));
            var stock=this.Stock.Get(new(w,i,0,0));
            var qty=1+this.rng.Next(10);
            if(stock.X>=qty+10){
                stock.X-=qty;
            }else{
                stock.X=stock.X-qty+91;
            }
            stock.Y+=qty;
            this.Stock.Replace(stock);
            if(stock.X<20){
                this.StockLow.Put(new(w,i,0,0),new V24{A=stock.X});
            }else{
                this.StockLow.Remove(new(w,i,0,0));
            }
            this.OrderLine.Insert(NewRow<Pad24>(new(w,d,o,n),i,qty,o*16+n));
        }
        this.CustomerLastOrder.Put(new(w,d,c,0),new V24{A=o});
        this.OrderLineCount.Put(new(w,d,o,0),new V24{A=lines});
        this.OrderCustomer.Put(new(w,d,o,0),new V24{A=c});
        if(!this.NewOrderMin.TryGet(new(w,d,0,0),out _)){
            this.NewOrderMin.Put(new(w,d,0,0),new V24{A=o});
        }
    }
    private void Payment(long w){
        var d=1+this.rng.Next(Districts);
        var amount=1+this.rng.Next(5000);
        var warehouse=this.Warehouse.Get(new(w,0,0,0));
        warehouse.Y+=amount;
        this.Warehouse.Replace(warehouse);
        var district=this.District.Get(new(w,d,0,0));
        district.Y+=amount;
        this.District.Replace(district);
        long c;
        if(this.rng.Next(100)<60){
            var last=this.rng.Next(100);
            if(!this.CustomerByLast.TryGet(new(w,d,last,0),out var entry)){
                return;
            }
            c=entry.B;
        }else{
            c=1+this.rng.Next(this.Scale.Customers);
        }
        var customer=this.Customer.Get(new(w,d,c,0));
        customer.X-=amount;
        Hashing.Fill(ref customer.Pad,customer.X);
        this.Customer.Replace(customer);
        this.historySeq++;
        this.History.Insert(NewRow<Pad16>(new(this.Id,this.historySeq,0,0),c,amount,this.historySeq));
        this.HistorySeq.Put(new(0,0,0,0),new V24{A=this.historySeq});
        var payKey=new K4(w,d,c,0);
        if(this.PaymentByCustomer.TryGet(payKey,out var paid)){
            paid.A++;
            paid.B+=amount;
            this.PaymentByCustomer.Put(payKey,paid);
        }else{
            this.PaymentByCustomer.Put(payKey,new V24{A=1,B=amount});
        }
    }
    private void Delivery(long w){
        var carrier=1+this.rng.Next(10);
        for(long d=1;d<=Districts;d++){
            var minKey=new K4(w,d,0,0);
            if(!this.NewOrderMin.TryGet(minKey,out var min)){
                continue;
            }
            var o=min.A;
            this.NewOrder.Delete(new(w,d,o,0));
            var order=this.Order.Get(new(w,d,o,0));
            order.Y=order.Y%1000+carrier*1000;
            this.Order.Replace(order);
            long lines=0;
            if(this.OrderLineCount.TryGet(new(w,d,o,0),out var count)){
                lines=count.A;
            }
            long total=0;
            for(long n=1;n<=lines;n++){
                var line=this.OrderLine.Get(new(w,d,o,n));
                line.Y+=1_000_000;
                total+=line.X;
                this.OrderLine.Replace(line);
            }
            var customer=this.Customer.Get(new(w,d,order.X,0));
            customer.X+=total;
            customer.Y++;
            this.Customer.Replace(customer);
            // 次の未配送の注文。無ければ索引から消す(辞書の Remove)。
            if(this.NewOrder.TryGet(new(w,d,o+1,0),out _)){
                this.NewOrderMin.Put(minKey,new V24{A=o+1});
            }else{
                this.NewOrderMin.Remove(minKey);
            }
        }
    }
    private void OrderStatus(long w){
        var d=1+this.rng.Next(Districts);
        var c=1+this.rng.Next(this.Scale.Customers);
        this.Customer.Get(new(w,d,c,0));
        if(!this.CustomerLastOrder.TryGet(new(w,d,c,0),out var last)){
            return;
        }
        var order=this.Order.Get(new(w,d,last.A,0));
        for(long n=1;n<=order.Y%1000;n++){
            this.OrderLine.Get(new(w,d,last.A,n));
        }
    }
    private void StockLevel(long w){
        var d=1+this.rng.Next(Districts);
        var district=this.District.Get(new(w,d,0,0));
        var next=district.X;
        for(var o=Math.Max(1,next-20);o<next;o++){
            if(!this.OrderLineCount.TryGet(new(w,d,o,0),out var count)){
                continue;
            }
            for(long n=1;n<=count.A;n++){
                var line=this.OrderLine.Get(new(w,d,o,n));
                this.Stock.Get(new(w,line.X,0,0));
            }
        }
    }
    public void ReleaseAll(){
        foreach(var part in this.parts){
            part.Release();
        }
        this.Meta.Release();
    }
    public void AcquireAll(){
        foreach(var part in this.parts){
            part.Acquire();
        }
        this.Meta.Acquire();
    }
    /// <summary>
    /// 保存する(呼ぶ側が区画を押さえていること)。模型があれば、各データ本体の要約を「meta」に書いてから保存する
    /// (保存したものが自分で正しさを確かめられるように)。<paramref name="releaseHandles"/> なら操作用オブジェクトを閉じて保存する。
    /// </summary>
    public SaveRecord Save(bool releaseHandles){
        this.serial++;
        Digest[]? expected=null;
        if(this.Options.Mirror){
            expected=new Digest[this.parts.Count];
            for(var index=0;index<this.parts.Count;index++){
                expected[index]=this.parts[index].Expected;
                this.Meta.Put(new(index+1,0,0,0),new V24{A=expected[index].Count,B=(long)expected[index].Sum,C=this.serial});
            }
        }
        this.Meta.Put(new(0,0,0,0),new V24{A=this.serial,B=this.parts.Count,C=this.Transactions});
        if(releaseHandles){
            this.ReleaseAll();
        }
        var watch=Stopwatch.StartNew();
        var token=this.Store.Save();
        var elapsed=watch.Elapsed;
        if(releaseHandles){
            this.AcquireAll();
        }
        return new(token,this.serial,expected,elapsed);
    }
    /// <summary>今の中身を全走査して、期待した要約と比べる。違えば説明を返す。</summary>
    public string? Verify(Digest[] expected){
        for(var index=0;index<this.parts.Count;index++){
            var part=this.parts[index];
            var scanned=part.Scan();
            if(scanned!=expected[index]){
                return $"p{this.Id} {part.Name}: scan={scanned} expected={expected[index]}";
            }
            var count=part.StoreCount();
            if(count!=expected[index].Count){
                return $"p{this.Id} {part.Name}: count={count} expected={expected[index].Count}";
            }
        }
        return null;
    }
    /// <summary>「meta」に書いた要約と今の中身を比べる。違えば説明を返す。</summary>
    public string? VerifySelf(){
        var expected=new Digest[this.parts.Count];
        for(var index=0;index<this.parts.Count;index++){
            if(!this.Meta.TryGet(new(index+1,0,0,0),out var entry)){
                return $"p{this.Id}: meta entry {index+1} is missing";
            }
            expected[index]=new(entry.A,(ulong)entry.B);
        }
        return this.Verify(expected);
    }
    public void Dispose(){
        this.ReleaseAll();
        this.Store.Dispose();
    }
}
