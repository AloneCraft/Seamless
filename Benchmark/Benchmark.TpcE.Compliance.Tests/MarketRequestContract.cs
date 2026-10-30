using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class MarketRequestContract{
    internal static (long PriceBits,long TradeId,int Quantity,int Action,string Symbol,string Type) Fields(byte[] bytes){
        Assert.Equal(TradeRequest.Size,bytes.Length);var message=new TradeRequest(bytes);
        return(BitConverter.DoubleToInt64Bits(message.PriceQuote),message.TradeId,message.TradeQty,message.EAction,message.Symbol,message.TradeTypeId);
    }
    [Fact]public void EveryAbiFieldIsComparedAndOnlyTailAlignmentIsExcluded(){
        var original=new byte[TradeRequest.Size];var message=new TradeRequest(original);message.PriceQuote=25;message.TradeId=123;message.TradeQty=10;message.EAction=1;message.Symbol="TEST";message.TradeTypeId="TMB";
        // 生成済みABIはフィールドが0..43、全長48。44..47はC++の未初期化になり得る末尾alignment。
        var padded=original.ToArray();padded.AsSpan(44,4).Fill(0xA5);Assert.Equal(Fields(original),Fields(padded));
        var changed=padded.ToArray();changed[16]^=1;Assert.NotEqual(Fields(original),Fields(changed));
    }
}
