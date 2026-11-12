using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class QueryTraceLayout{
    [Theory][InlineData(false)][InlineData(true)]
    public void NearbyObservationsKeepTheirCoordinatesAndSeparateExplanationRows(bool ordered){
        var first=Guid.NewGuid();var second=Guid.NewGuid();
        var frames=new[]{Frame(first,1,0),Frame(first,2,10),Frame(first,3,20),Frame(first,4,50000),Frame(second,1,0),Frame(second,2,10),Frame(second,3,20),Frame(second,4,50000)};
        var layout=トレース配置.作成(frames,ordered);
        Assert.Equal(8,layout.点.Length);Assert.Equal(8,layout.説明.Sum(label=>label.観測.Length));Assert.Equal(0,layout.表示省略数);Assert.Equal(0,layout.範囲外数);
        foreach(var point in layout.点){
            var fraction=point.値.Elapsed.Ticks/50000d;if(ordered){fraction=(point.値.Sequence-1)/3d;}
            Assert.Equal(72+fraction*240,point.Y,8);
        }
        Assert.InRange(layout.目盛.Length,1,7);Assert.Equal(layout.目盛.Length,layout.目盛.Select(tick=>tick.値).Distinct().Count());
        CheckLabels(layout);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void Dense1024FramesKeepEveryPointAndBoundAggregationDisplacementAndExtent(bool ordered){
        var id=Guid.NewGuid();var frames=Enumerable.Range(1,1024).Select(sequence=>Frame(id,sequence,0)).ToArray();
        var layout=トレース配置.作成(frames,ordered);
        Assert.Equal(1024,layout.記録.Length);Assert.Equal(1024,layout.点.Length);Assert.Equal(1024,layout.説明.Sum(label=>label.観測.Length));
        Assert.True(layout.集約観測数>0);Assert.Equal(0,layout.表示省略数);Assert.Equal(0,layout.範囲外数);
        Assert.InRange(layout.高さ,360,7400);Assert.Equal(640,layout.幅);
        if(!ordered){Assert.All(layout.点,point=>Assert.Equal(72,point.Y));Assert.Equal(360,layout.高さ);Assert.Equal(4,layout.説明.Length);}
        CheckLabels(layout);
    }

    [Fact]public void InvalidCoordinatesAndTheDisplayLimitAreSeparateFromExplanationAggregation(){
        var id=Guid.NewGuid();var frames=Enumerable.Range(1,1025).Select(sequence=>Frame(id,sequence,0)).ToArray();
        frames[0]=frames[0] with{StartedAtUtc=DateTimeOffset.MaxValue,Elapsed=TimeSpan.FromTicks(1)};
        var layout=トレース配置.作成(frames,false);
        Assert.Equal(1024,layout.記録.Length);Assert.Equal(1023,layout.点.Length);Assert.Equal(1,layout.範囲外数);Assert.Equal(1,layout.表示省略数);
        Assert.Equal(1023,layout.説明.Sum(label=>label.観測.Length));Assert.True(layout.集約観測数>0);
    }

    private static void CheckLabels(トレース配置 layout){
        foreach(var lane in layout.説明.GroupBy(label=>label.観測[0].値.ExecutionId)){
            var labels=lane.OrderBy(label=>label.Y).ToArray();
            for(var index=1;index<labels.Length;index++){Assert.True(labels[index].Y-labels[index-1].Y>=トレース配置.行間隔-0.0001);}
            foreach(var label in labels){
                Assert.All(label.観測,point=>Assert.InRange(label.Y-point.Y,0,トレース配置.最大退避));
                Assert.True(label.X+トレース配置.説明幅<label.観測[0].X+トレース配置.レーン幅);
                Assert.True(label.Y+10<layout.高さ);
            }
        }
    }
    private static QueryTraceFrame Frame(Guid id,int sequence,long ticks)=>new(id,sequence,"definition","query",1,new('A',64),true,DateTimeOffset.UnixEpoch,TimeSpan.FromTicks(ticks),"Sample","Execution",0,null,QueryTraceRecordMode.TimeSampled,QueryTraceReliability.BestEffort);
}
