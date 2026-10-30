using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
public sealed class Rng{
    [Fact,Trait("Category","Unit")]public void For(){
        {
            //同じ (種, 流れ, 番号) なら同じ列
            var left=Core.Rng.For(1,"ce",42);
            var right=Core.Rng.For(1,"ce",42);
            for(var index=0;index<100;index++){
                Assert.Equal(left.NextUInt64(),right.NextUInt64());
            }
        }
        {
            //違う流れなら違う列
            var left=Core.Rng.For(1,"ce",42);
            var right=Core.Rng.For(1,"mee",42);
            Assert.NotEqual(left.NextUInt64(),right.NextUInt64());
        }
    }
    [Fact,Trait("Category","Unit")]public void Range(){
        {
            //範囲の両端を含み、外へ出ない
            var rng=new Core.Rng(7);
            var seen=new bool[6];
            for(var index=0;index<10_000;index++){
                var value=rng.Range(5,10);
                Assert.InRange(value,5,10);
                seen[value-5]=true;
            }
            Assert.All(seen,Assert.True);
        }
    }
}
