using System.Text;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// 評価回数の検査(<see cref="QueryEvaluationCheck"/>)を、全組み合わせに対して調べるための調査用のテスト。
/// 通常は何もしない。環境変数 EVAL_SURVEY に出力先のファイル名(%TEMP% の下)を入れたときだけ動き、
/// 最適化ありの FLinq が System.Linq より多くラムダを評価した並びと、その回数を書き出す。
/// EVAL_SURVEY_STEP は 2 個の中間演算子の並びを何件おきに調べるか(既定 3)。EVAL_SURVEY_INTERMEDIATES は 1 か 2(既定 両方)。
/// EVAL_SURVEY_KEY に「a > b > 終端」(;区切り)を入れると、その並びだけを調べて、式・入力・各経路の回数と結果・FLinq の展開後のコードも書き出す。
/// 修正のたびに、この結果が減ることを確かめて、厳密に比べる範囲(DynamicEvaluationCountTests)を広げる。
/// </summary>
public sealed class DynamicEvaluationCountSurvey{
    private const int InputElementCount=16;

    private static readonly string[] Terminals=[
        QueryOperatorIds.ToArray,
        QueryOperatorIds.Count,
        QueryOperatorIds.Sum,
        QueryOperatorIds.FirstOrDefault,
        QueryOperatorIds.Any,
    ];

    [Fact,Trait("Category","Survey")]
    public void 評価回数がSystemLinqより多い並びを書き出す(){
        var outputName=Environment.GetEnvironmentVariable("EVAL_SURVEY");
        if(string.IsNullOrWhiteSpace(outputName)){
            return;
        }
        var step=int.TryParse(Environment.GetEnvironmentVariable("EVAL_SURVEY_STEP"),out var parsedStep)?Math.Max(1,parsedStep):3;
        var only=Environment.GetEnvironmentVariable("EVAL_SURVEY_INTERMEDIATES");
        var detailKeys=(Environment.GetEnvironmentVariable("EVAL_SURVEY_KEY")??string.Empty).Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var report=new StringBuilder();
        var examined=0;
        var skipped=0;
        var excess=0;
        var unloadFailures=0;
        foreach(var count in new[]{1,2}){
            if(only is "1" or "2"&&only!=count.ToString()){
                continue;
            }
            foreach(var spec in ExhaustiveQueryEnumerator.Enumerate(count,intermediates,Terminals)){
                if(count==2&&spec.Ordinal%step!=0||!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||!QueryEvaluationCheck.Applies(query!)){
                    continue;
                }
                var key=$"{string.Join(" > ",spec.Operators)} > {spec.Terminal}";
                if(detailKeys.Count>0&&!detailKeys.Contains(key)){
                    continue;
                }
                examined++;
                using var fixture=new SourceFixtureFactory().Create(target,InputElementCount,SeedDerivation.Derive(seed,"fixture",(int)spec.Ordinal));
                var emitted=new QuerySourceEmitter().Emit(query!,new QuerySourceEmissionOptions{GeneratedNamespace="Survey",ClassName="Probe"});
                var counted=EvaluationProbeRewriter.Instrument(emitted.SourceText);
                var compiled=new DynamicQueryCompiler().Compile(new DynamicCompilationRequest("Survey."+Guid.NewGuid().ToString("N"),"Survey.Probe",counted,"Survey.g.cs"));
                if(compiled.LoadedQuery is not { } loaded){
                    skipped++;
                    report.AppendLine($"SKIP {key}");
                    continue;
                }
                var counts=new Dictionary<DynamicImplementationKind,long>();
                var byLambda=new Dictionary<DynamicImplementationKind,long[]>();
                if(detailKeys.Count>0){
                    report.AppendLine("=== "+key+"  "+Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query!));
                    report.AppendLine("primary="+string.Join(",",(int[])fixture.Primary!));
                }
                foreach(var kind in new[]{DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq,DynamicImplementationKind.ZLinq}){
                    if(!loaded.Has(kind)){
                        continue;
                    }
                    EvaluationProbe.Reset();
                    var outcome=string.Empty;
                    try{
                        var value=loaded.Run(kind,fixture.Primary,fixture.Secondary);
                        outcome=string.Join(",",value.Items.Select(static item=>item.ToString()));
                    }catch(Exception exception){
                        outcome=exception.GetType().Name;
                    }
                    counts[kind]=EvaluationProbe.Count;
                    byLambda[kind]=EvaluationProbe.CountsByLambda;
                    if(detailKeys.Count>0){
                        report.AppendLine($"{kind}: {counts[kind]} 回 [{string.Join(",",byLambda[kind])}]  結果={outcome}");
                    }
                }
                try{
                    if(!loaded.UnloadAndVerify().Unloaded){
                        unloadFailures++;
                        report.AppendLine("UNLOAD_FAILURE "+key);
                    }
                }catch(Exception exception){
                    unloadFailures++;
                    report.AppendLine($"UNLOAD_FAILURE {key}  {exception.GetType().Name}: {exception.Message}");
                }
                if(detailKeys.Count>0){
                    report.AppendLine(compiled.ExpandedSource);
                }
                if(EvaluationCountComparer.CompareByLambda(byLambda) is not null){
                    excess++;
                    report.AppendLine($"EXCESS {key}  System={counts[DynamicImplementationKind.System]} [{string.Join(",",byLambda[DynamicImplementationKind.System])}] FLinq={counts[DynamicImplementationKind.FLinq]} [{string.Join(",",byLambda[DynamicImplementationKind.FLinq])}]"
                        +(counts.TryGetValue(DynamicImplementationKind.ZLinq,out var z)?$" ZLinq={z}":string.Empty));
                }
            }
        }
        report.Insert(0,$"examined={examined} skipped={skipped} excess={excess} unloadFailures={unloadFailures}{Environment.NewLine}");
        var outputPath=Path.Combine(Path.GetTempPath(),outputName);
        File.WriteAllText(outputPath,report.ToString());
        if(Environment.GetEnvironmentVariable("EVAL_SURVEY_REQUIRE_CLEAN")=="1"){
            Assert.True(examined>0&&skipped==0&&excess==0&&unloadFailures==0,
                $"Survey was not clean: unloadFailures={unloadFailures}, examined={examined}, skipped={skipped}, excess={excess}. Report: {outputPath}");
        }
    }
}
