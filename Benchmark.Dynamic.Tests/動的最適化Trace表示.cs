using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

public sealed class 動的最適化Trace表示Tests{
    [Fact,Trait("Category","Unit")]
    public void LayerRoundEventのtreeとBeforeAfter差分へ変換する(){
        var trace=JsonSerializer.Serialize(new{
            SchemaVersion=1,
            MethodKey="C.M()",
            Events=new[]{new{
                Sequence=1,
                RunId=2,
                Layer="Syntax",
                Phase="Algebraic",
                RuleId="CountComparisonToAny",
                Round=3,
                Pass=4,
                VisitNumber=5,
                NodePath="/1/0",
                Depth=(int?)2,
                StageIndex=(int?)null,
                Left=true,
                Right=false,
                Inner=false,
                Revisited=true,
                Before="Count() > 0",
                After="Any()",
            }},
            LayerTerminations=new[]{new{
                RunId=2,
                Layer="Syntax",
                RoundCount=4,
                Termination="FixedPoint",
            }},
            Summary=new{
                RewriteCount=1,
                OptimizationPassCount=4,
                ReachedFixedPoint=true,
            },
        });
        var outer=JsonSerializer.Serialize(new[]{trace});

        var rows=global::Benchmark.Dynamic.動的最適化Trace表示Parser.解析(outer);

        Assert.Collection(
            rows,
            row=>Assert.Equal(global::Benchmark.Dynamic.動的最適化Trace表示種別.Document,row.種別),
            row=>{
                Assert.Equal(global::Benchmark.Dynamic.動的最適化Trace表示種別.Layer,row.種別);
                Assert.Contains("FixedPoint",row.詳細,StringComparison.Ordinal);
            },
            row=>{
                Assert.Equal(global::Benchmark.Dynamic.動的最適化Trace表示種別.Round,row.種別);
                Assert.Contains("Round 3",row.見出し,StringComparison.Ordinal);
            },
            row=>{
                Assert.Equal(global::Benchmark.Dynamic.動的最適化Trace表示種別.Event,row.種別);
                Assert.Equal("Count() > 0",row.Before);
                Assert.Equal("Any()",row.After);
                Assert.True(row.差分あり);
                Assert.Contains("left",row.詳細,StringComparison.Ordinal);
                Assert.Contains("revisited",row.詳細,StringComparison.Ordinal);
            });
    }

    [Fact,Trait("Category","Unit")]
    public void 壊れたJSONはError行として表示する(){
        var row=Assert.Single(global::Benchmark.Dynamic.動的最適化Trace表示Parser.解析("{"));
        Assert.Equal(global::Benchmark.Dynamic.動的最適化Trace表示種別.Error,row.種別);
    }
}
