namespace Benchmark.TpcC.Core;
/// <summary>
/// 規模。TPC-C 5.11 の 4.2〜4.3: 倉庫 W 個に対して、地区は倉庫あたり 10、顧客は地区あたり 3,000、品目は 100,000(倉庫数に依らない)。
/// 試験と不具合探しのために、品目数と地区あたりの顧客数は縮小できる(仕様外。既定は仕様どおり)。
/// 区画(倉庫を束ねた単位)の数 <see cref="Partitions"/> は同時に動かすコアの数。倉庫は区画に連続した範囲で割り当てる。
/// </summary>
public sealed record TpcCScale{
    public const int DistrictsPerWarehouse=10;
    public int Warehouses{get;init;}=1;
    /// <summary>区画の数。倉庫の数を超える分は使われない(<see cref="PartitionCount"/>)。</summary>
    public int Partitions{get;init;}=1;
    /// <summary>品目の数(仕様は 100,000)。</summary>
    public int Items{get;init;}=100_000;
    /// <summary>地区あたりの顧客数(仕様は 3,000)。初期の注文も同じ数。</summary>
    public int CustomersPerDistrict{get;init;}=3_000;
    /// <summary>実行中に流す新規注文の見込み(索引と予算の見積もりに使う)。</summary>
    public long ExpectedRunOrders{get;init;}=10_000;
    public int PartitionCount=>Math.Max(1,Math.Min(this.Partitions,this.Warehouses));
    /// <summary>地区あたりの初期の注文数(顧客ごとに 1 件)。</summary>
    public int InitialOrdersPerDistrict=>this.CustomersPerDistrict;
    /// <summary>初期の未配送の注文の最初の番号(仕様は 3,000 件中の 2,101 以降 = 末尾の 30%)。</summary>
    public int FirstInitialNewOrder=>this.CustomersPerDistrict-this.CustomersPerDistrict*3/10+1;
    /// <summary>倉庫 w(1 始まり)を持つ区画の番号(0 始まり)。倉庫を連続した範囲で割り当てる。</summary>
    public int PartitionOf(int warehouse){
        if(warehouse<1||warehouse>this.Warehouses){
            throw new ArgumentOutOfRangeException(nameof(warehouse),warehouse,"warehouse is out of range.");
        }
        return (int)((long)(warehouse-1)*this.PartitionCount/this.Warehouses);
    }
    /// <summary>区画 p が持つ倉庫の範囲 [First,Last](1 始まり)。</summary>
    public (int First,int Last) WarehousesOf(int partition){
        if(partition<0||partition>=this.PartitionCount){
            throw new ArgumentOutOfRangeException(nameof(partition),partition,"partition is out of range.");
        }
        var first=0;
        var last=0;
        var found=false;
        for(var warehouse=1;warehouse<=this.Warehouses;warehouse++){
            if(this.PartitionOf(warehouse)!=partition){
                continue;
            }
            if(!found){
                first=warehouse;
                found=true;
            }
            last=warehouse;
        }
        return (first,last);
    }
    public long Customers=>(long)this.Warehouses*DistrictsPerWarehouse*this.CustomersPerDistrict;
    public long Stocks=>(long)this.Warehouses*this.Items;
    /// <summary>区画あたりの倉庫数の上限(表の初期容量の見積もりに使う)。</summary>
    public int MaxWarehousesPerPartition=>(this.Warehouses+this.PartitionCount-1)/this.PartitionCount;
    /// <summary>NURand の A(顧客 ID)。範囲が 3,000 のときは仕様の 1,023。</summary>
    public int CustomerIdA=>NuRandA(this.CustomersPerDistrict,1023,3_000);
    /// <summary>NURand の A(品目 ID)。範囲が 100,000 のときは仕様の 8,191。</summary>
    public int ItemIdA=>NuRandA(this.Items,8191,100_000);
    /// <summary>範囲が仕様の大きさなら仕様の A を、縮小したときは範囲の半分以下で最大の 2^k-1 を返す。</summary>
    private static int NuRandA(int range,int specA,int specRange){
        if(range==specRange){
            return specA;
        }
        var a=1;
        while(a*2+1<=range/2){
            a=a*2+1;
        }
        return a;
    }
}
