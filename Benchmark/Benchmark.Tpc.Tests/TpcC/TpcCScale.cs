namespace Benchmark.TpcC.Tests;
public sealed class TpcCScale{
    [Fact,Trait("Category","Unit")]public void PartitionOf(){
        {
            //倉庫は連続した範囲で区画に割り当てられ、区画の倉庫数は高々 1 しか違わない
            var scale=new Core.TpcCScale{Warehouses=10,Partitions=3};
            var previous=0;
            var counts=new int[3];
            for(var warehouse=1;warehouse<=10;warehouse++){
                var partition=scale.PartitionOf(warehouse);
                Assert.InRange(partition-previous,0,1);
                previous=partition;
                counts[partition]++;
            }
            Assert.Equal(2,previous);
            Assert.InRange(counts.Max()-counts.Min(),0,1);
        }
        {
            //区画の数が倉庫の数を超えるときは倉庫の数に丸める
            var scale=new Core.TpcCScale{Warehouses=2,Partitions=8};
            Assert.Equal(2,scale.PartitionCount);
            Assert.Equal(0,scale.PartitionOf(1));
            Assert.Equal(1,scale.PartitionOf(2));
        }
        {
            //範囲外の倉庫は受け付けない
            var scale=new Core.TpcCScale{Warehouses=4,Partitions=2};
            Assert.Throws<ArgumentOutOfRangeException>(()=>scale.PartitionOf(0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>scale.PartitionOf(5));
        }
    }
    [Fact,Trait("Category","Unit")]public void WarehousesOf(){
        {
            //PartitionOf と矛盾せず、全倉庫を過不足なく覆う
            var scale=new Core.TpcCScale{Warehouses=11,Partitions=4};
            var seen=new List<int>();
            for(var partition=0;partition<scale.PartitionCount;partition++){
                var (first,last)=scale.WarehousesOf(partition);
                for(var warehouse=first;warehouse<=last;warehouse++){
                    Assert.Equal(partition,scale.PartitionOf(warehouse));
                    seen.Add(warehouse);
                }
            }
            Assert.Equal(Enumerable.Range(1,11),seen);
        }
    }
    [Fact,Trait("Category","Unit")]public void CustomerIdA(){
        {
            //仕様の大きさでは仕様の A(顧客 1,023・品目 8,191)、縮小したときは範囲の半分以下の 2^k-1
            var spec=new Core.TpcCScale();
            Assert.Equal(1023,spec.CustomerIdA);
            Assert.Equal(8191,spec.ItemIdA);
            var mini=TestDatabases.Mini(1,1);
            Assert.Equal(15,mini.CustomerIdA);
            Assert.Equal(63,mini.ItemIdA);
        }
    }
    [Fact,Trait("Category","Unit")]public void FirstInitialNewOrder(){
        {
            //仕様では地区あたり 3,000 件の注文のうち 2,101 番以降の 900 件が未配送
            var spec=new Core.TpcCScale();
            Assert.Equal(2101,spec.FirstInitialNewOrder);
            Assert.Equal(900,spec.InitialOrdersPerDistrict-spec.FirstInitialNewOrder+1);
        }
        {
            //縮小しても末尾の 30% が未配送
            var mini=TestDatabases.Mini(1,1);
            Assert.Equal(22,mini.FirstInitialNewOrder);
        }
    }
}
