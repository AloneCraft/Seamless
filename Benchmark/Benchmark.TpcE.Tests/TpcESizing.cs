namespace Benchmark.TpcE.Tests;
public sealed class TpcESizing{
    [Fact]public void LegacySegmentsRemainDefaultAndLargeSegmentsAreExplicit(){
        var legacy=new Core.TpcEScale();var large=legacy with{TsavoriteSegmentBytes=16L*1024*1024};
        Assert.Equal(4096,Core.TpcESizing.TableTuning("trade",57600,legacy).SegmentSize);
        Assert.Equal(4096,Core.TpcESizing.IndexTuning("x06.c",legacy).SegmentSize);
        Assert.Equal(16L*1024*1024,Core.TpcESizing.TableTuning("trade",57600,large).SegmentSize);
        Assert.Equal(16L*1024*1024,Core.TpcESizing.IndexTuning("x06.c",large).SegmentSize);
        Assert.Equal(Core.TpcESizing.TableTuning("trade",57600,legacy).LogMemorySize,Core.TpcESizing.TableTuning("trade",57600,large).LogMemorySize);
        Assert.DoesNotContain("TsavoriteSegmentBytes",System.Text.Json.JsonSerializer.Serialize(legacy));
    }
    [Theory,InlineData(0),InlineData(-1),InlineData(5000)]public void InvalidExplicitSegmentIsRejected(long bytes){
        var scale=new Core.TpcEScale{TsavoriteSegmentBytes=bytes};
        Assert.Throws<ArgumentOutOfRangeException>(()=>Core.TpcESizing.TableTuning("trade",1,scale));
        Assert.Throws<ArgumentOutOfRangeException>(()=>Core.TpcESizing.IndexTuning("x06.c",scale));
    }
    [Theory][InlineData(64)][InlineData(100000)]public void NewsItemSegmentContainsItsPage(int bodyBytes){
        var tuning=Core.TpcESizing.TableTuning("news_item",1000,new Core.TpcEScale{NewsItemBytes=bodyBytes});
        Assert.True(tuning.SegmentSize>=tuning.PageSize);Assert.True(tuning.LogMemorySize>=2*tuning.PageSize);
    }
}
