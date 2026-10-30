using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
public sealed class CanonicalWriter{
    private readonly record struct Sample(long Id,decimal Amount,string? Name,Core.Code8 Code,int[] Values);
    private readonly record struct WithDouble(double Value);
    [Fact,Trait("Category","Unit")]public void ToBytes(){
        {
            //decimal は値で正規化する(1.0 と 1.00、0 と -0 は同じ)
            Assert.Equal(Core.CanonicalWriter.ToBytes(1.0m),Core.CanonicalWriter.ToBytes(1.00m));
            Assert.Equal(Core.CanonicalWriter.ToBytes(0m),Core.CanonicalWriter.ToBytes(-0.00m));
            Assert.NotEqual(Core.CanonicalWriter.ToBytes(1.0m),Core.CanonicalWriter.ToBytes(1.01m));
        }
        {
            //記録型は全項目を書く(1 項目違えば違う)
            var left=new Sample(1,2.50m,"x",Core.Code8.From("A"),[1,2]);
            Assert.Equal(Core.CanonicalWriter.ToBytes(left),Core.CanonicalWriter.ToBytes(left with{Amount=2.5m}));
            Assert.NotEqual(Core.CanonicalWriter.ToBytes(left),Core.CanonicalWriter.ToBytes(left with{Name=null}));
            Assert.NotEqual(Core.CanonicalWriter.ToBytes(left),Core.CanonicalWriter.ToBytes(left with{Values=[2,1]}));
            Assert.NotEqual(Core.CanonicalWriter.ToBytes(left),Core.CanonicalWriter.ToBytes(left with{Code=Core.Code8.From("B")}));
        }
        {
            //double は比べられないので例外
            Assert.Throws<NotSupportedException>(()=>Core.CanonicalWriter.ToBytes(new WithDouble(1)));
        }
    }
}
