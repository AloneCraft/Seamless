using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class Seed256Tests{
    [Fact,Trait("Category","Unit")]public void TryParse(){
        {
            Assert.True(Core.Seed256.TryParse("18446744073709551615",out var decimalSeed));
            Assert.True(Core.Seed256.TryParse("0xFFFFFFFFFFFFFFFF",out var hexadecimalSeed));
            Assert.Equal(decimalSeed,hexadecimalSeed);
            Assert.Equal(64,decimalSeed.ToCanonicalString().Length);
            Assert.Equal(decimalSeed,Core.Seed256.Parse(decimalSeed.ToCanonicalString()));
        }
        {
            Assert.False(Core.Seed256.TryParse("-1",out _));
            Assert.False(Core.Seed256.TryParse("0x10000000000000000",out _));
            Assert.False(Core.Seed256.TryParse("not-a-seed",out _));
            Assert.Throws<FormatException>(()=>Core.Seed256.Parse("not-a-seed"));
        }
    }
}
