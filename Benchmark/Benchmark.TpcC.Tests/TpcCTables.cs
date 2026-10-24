using System.Runtime.CompilerServices;
namespace Benchmark.TpcC.Tests;
public sealed class TpcCTables{
    [Fact,Trait("Category","Unit")]public void KeysHaveNoPadding(){
        {
            //Tsavorite と Persisted は鍵をバイト列で比べるので、鍵に詰め物が入ってはいけない
            Assert.Equal(4,Unsafe.SizeOf<Core.WarehouseKey>());
            Assert.Equal(8,Unsafe.SizeOf<Core.DistrictKey>());
            Assert.Equal(12,Unsafe.SizeOf<Core.CustomerKey>());
            Assert.Equal(12,Unsafe.SizeOf<Core.HistoryKey>());
            Assert.Equal(12,Unsafe.SizeOf<Core.NewOrderKey>());
            Assert.Equal(12,Unsafe.SizeOf<Core.OrderKey>());
            Assert.Equal(16,Unsafe.SizeOf<Core.OrderLineKey>());
            Assert.Equal(4,Unsafe.SizeOf<Core.ItemKey>());
            Assert.Equal(8,Unsafe.SizeOf<Core.StockKey>());
        }
    }
    [Fact,Trait("Category","Unit")]public void IndexOwnersHaveNoPadding(){
        {
            //索引の持ち主は 8 バイトの倍数で、チャンクの鍵(持ち主 + 番号)にも詰め物が入らない
            AssertNoPadding<Core.DistrictOwner>(8);
            AssertNoPadding<Core.CustomerOwner>(16);
            AssertNoPadding<Core.CustomerNameOwner>(24);
            Assert.Equal(24,Unsafe.SizeOf<Core.CustomerNameEntry>());
        }
    }
    private static void AssertNoPadding<TOwner>(int size) where TOwner:unmanaged{
        Assert.Equal(size,Unsafe.SizeOf<TOwner>());
        Assert.Equal(0,size%8);
        Assert.Equal(size+8,Unsafe.SizeOf<ChunkKey<TOwner>>());
    }
    [Fact,Trait("Category","Unit")]public void DistInfoOf(){
        {
            //地区 1〜10 の S_DIST_xx を返し、範囲外は例外
            var stock=new Core.StockRow(1,1,50,"d1","d2","d3","d4","d5","d6","d7","d8","d9","d10",0,0,0,"data");
            for(var district=1;district<=10;district++){
                Assert.Equal("d"+district,stock.DistInfoOf(district));
            }
            Assert.Throws<ArgumentOutOfRangeException>(()=>stock.DistInfoOf(0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>stock.DistInfoOf(11));
        }
    }
}
