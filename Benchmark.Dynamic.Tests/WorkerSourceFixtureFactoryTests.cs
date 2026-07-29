using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

public sealed class SourceFixtureFactoryTests{
    [Fact,Trait("Category","Unit")]
    public void Create_CreatesFreshEquivalentFixturesForEverySourceTarget(){
        var factory=new SourceFixtureFactory();
        var seed=Seed256.FromUInt64(1234);
        foreach(var target in Enum.GetValues<SourceTarget>()){
            using var first=factory.Create(target,32,seed);
            using var second=factory.Create(target,32,seed);
            Assert.NotSame(first,second);
            Assert.NotSame(first.Primary,second.Primary);
            Assert.Equal(Enumerable.Range(first.PrimaryStart,32),first.PrimaryValues);
            Assert.Equal(first.PrimaryValues,second.PrimaryValues);
            Assert.Equal(first.SecondaryValues,second.SecondaryValues);
            Assert.Equal(target.RequiresSameThread(),first.RequiresSameThread);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Create_DisposeHonorsProviderLifetimeAndThreadAffinity(){
        var factory=new SourceFixtureFactory();
        var seed=Seed256.FromUInt64(4567);
        foreach(var target in new[]{
            SourceTarget.SystemPersisted,
            SourceTarget.SeamlessPersisted,
            SourceTarget.SystemTsavorite,
            SourceTarget.SeamlessTsavorite,
        }){
            var fixture=factory.Create(target,8,seed);
            Assert.False(fixture.IsDisposed);
            fixture.Dispose();
            Assert.True(fixture.IsDisposed);
            fixture.Dispose();
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 最大入力も中央寄せした一意値でSumの安全域を保つ(){
        using var fixture=new SourceFixtureFactory().Create(
            SourceTarget.SystemArray,
            SourceFixtureFactory.MaximumInputElementCount,
            Seed256.Parse("0x515AFE"));

        Assert.Equal(SourceFixtureFactory.MaximumInputElementCount,fixture.PrimaryValues.Distinct().Count());
        Assert.InRange(fixture.PrimaryValues[0],-50_008,-49_992);
        Assert.InRange(fixture.PrimaryValues[^1],49_991,50_007);
        Assert.InRange(fixture.PrimaryValues.Sum(static value=>(long)value),-800_000L,800_000L);
    }
}
