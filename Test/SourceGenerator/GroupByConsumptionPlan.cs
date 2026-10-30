using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

public sealed class GroupByConsumptionPlan{
    [Fact]public void Create(){
        var candidate=new GroupByAggregatePlan{ResultType="int"};
        var prefix=global::Seamless.FLinq.Generator.GroupByConsumptionPlan.Create(new(DemandWindow.Prefix(CountExpr.Constant(2)),true,true,0));
        var all=global::Seamless.FLinq.Generator.GroupByConsumptionPlan.Create(new(DemandWindow.All,true,true,0));
        var first=candidate.WithConsumption(prefix);var second=candidate.WithConsumption(all);
        Assert.Null(candidate.GroupLimit);Assert.Null(second.GroupLimit);
        Assert.True(first.GroupLimit!.TryGetConstant(out var limit));Assert.Equal(2,limit);
        Assert.False(global::Seamless.FLinq.Generator.GroupByConsumptionPlan.Create(new(DemandWindow.Unknown,true,true,1)).Fuse);
    }
}
