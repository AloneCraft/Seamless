using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class PlanElementMeaning{
    [Fact]public async Task KnownOriginalOperatorsExplainTheirGeneralMeaningWithoutInterpretingTheCondition(){
        var where=new ExecutionPlanNode("source123_45","SourceOperator","Where",[],null,"query.cs",123,45,new(){{"Expression","source.Where(value => CustomCondition(value))"}});
        var html=await Render(where,"ja");
        Assert.Contains("この要素の意味",html);Assert.Contains("条件を満たす要素を選びます。",html);
        Assert.Contains("UTF16の開始位置と長さ",html);Assert.Contains("記録されたExpressionと対応コード",html);
        Assert.DoesNotContain("CustomCondition",html);
        var array=where with{Label="ToArray"};
        Assert.Contains("結果を配列として実体化します。",await Render(array,"ja"));
        Assert.Contains("Materializes the result as an array.",await Render(array,"en"));
    }
    [Fact]public async Task SelectedAndUnknownKindsUseRecordedMetadataWithoutGuessingFromTheirLabel(){
        var selected=new ExecutionPlanNode("fragment0","Fragment","Where",[],null,null,0,0,[]);
        var html=await Render(selected,"ja");
        Assert.Contains("FLinqが採用した処理の構成要素",html);Assert.DoesNotContain("条件を満たす要素を選びます。",html);
        var expression=selected with{Kind="Expression"};
        Assert.Contains("独立した実行タスクを意味しません",await Render(expression,"ja"));
        var unknown=selected with{Kind="FutureKind"};
        Assert.Contains("この種類の一般説明は未登録",await Render(unknown,"ja"));
        Assert.Contains("未収集の時間・CPU・I/Oをゼロとは扱いません",html);
    }
    private static async Task<string> Render(ExecutionPlanNode node,string culture){
        var previous=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(culture);
            using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async()=>{
                var output=await renderer.RenderComponentAsync<計画要素説明>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"ノード",node}}));
                return WebUtility.HtmlDecode(output.ToHtmlString());
            });
        }finally{CultureInfo.CurrentUICulture=previous;}
    }
}
