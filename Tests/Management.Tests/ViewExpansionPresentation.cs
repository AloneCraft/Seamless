using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ViewExpansionPresentation{
    [Theory]
    [InlineData("ja","旧形式","SDK")]
    [InlineData("en","legacy definition","SDK")]
    public async Task UnsupportedViewsExplainTheRequiredAction(string language,string expected,string action){
        var html=await Render(language,new(){{"ExpansionStatus","NotExpanded"},{"ExpansionReason","LegacyCreateViewCacheBoundary"}});
        Assert.Contains(expected,html);Assert.Contains(action,html);
        Assert.Contains("role=\"status\"",html);Assert.Contains("<strong",html);Assert.Contains("data-node-id=\"v1\"",html);
    }

    [Fact]
    public async Task ExpandedBodiesDiscloseRemainingKeyOptimization(){
        var html=await Render("ja",new(){{"ExpansionStatus","ExpandedDefinitionWithSetBoundary"}});
        Assert.Contains("定義本体は展開済み",html);Assert.Contains("キー検索",html);Assert.Contains("未対応",html);
    }

    [Fact]
    public async Task UnknownReasonsRemainVisibleAndEscaped(){
        var html=await Render("en",new(){{"ExpansionStatus","NotExpanded"},{"ExpansionReason","<new-reason>"}},decode:false);
        Assert.Contains("Unsupported view expansion reason",html);
        Assert.Contains("&lt;new-reason&gt;",html);Assert.DoesNotContain("<new-reason>",html);
    }

    [Fact]
    public async Task OrdinaryPlansHaveNoViewWarning(){
        Assert.Equal("",(await Render("ja",new())).Trim());
    }

    [Fact]
    public async Task AppliedPredicatesDoNotReportMissingPushdown(){
        Assert.Equal("",(await Render("ja",new(){{"ExpansionStatus","ExpandedDefinitionWithSetBoundary"},{"PredicatePushdownStatus","Applied"}})).Trim());
    }

    [Fact]
    public async Task RejectedPredicatesShowTheRecordedReason(){
        var html=await Render("en",new(){{"ExpansionStatus","ExpandedDefinitionWithSetBoundary"},{"PredicatePushdownStatus","NotApplied"},{"PredicatePushdownReason","ObservableProjection"}});
        Assert.Contains("ObservableProjection",html);Assert.Contains("could not be pushed",html);
    }

    [Fact]
    public async Task PredicateMovementDisclosesTheRemainingPointLookupStage(){
        var html=await Render("en",new(){{"ExpansionStatus","ExpandedDefinitionWithSetBoundary"},{"PredicatePushdownStatus","Applied"},{"PredicatePushdownReason","DirectKeyFilterBeforeProjection;PointLookupNotSelected"}});
        Assert.Contains("condition is pushed",html);Assert.Contains("point lookup is not implemented",html);
    }

    [Theory]
    [InlineData("ja","副作用・例外")]
    [InlineData("en","side effects and exceptions")]
    public async Task KnownRejectionsExplainTheSemanticBoundary(string language,string expected){
        var html=await Render(language,new(){{"ExpansionStatus","ExpandedDefinitionWithSetBoundary"},{"PredicatePushdownStatus","NotApplied"},{"PredicatePushdownReason","ProjectionConstructorOrEqualityNotProvenTotal"}});
        Assert.Contains(expected,html);Assert.DoesNotContain("ProjectionConstructorOrEqualityNotProvenTotal",html);
    }

    private static async Task<string> Render(string language,Dictionary<string,string> properties,bool decode=true){
        var culture=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(language);
            using var provider=new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
            var plan=new global::Seamless.Management.ExecutionPlanDocument(2,"Query","1","source","body",[new("v1","Fragment","OpaqueViewRoot",[],null,null,0,0,properties)],JsonSerializer.SerializeToElement(new{}));
            var html=await renderer.Dispatcher.InvokeAsync(async()=>{
                var root=await renderer.RenderComponentAsync<ビュー展開通知>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"計画",plan}}));
                return root.ToHtmlString();
            });
            if(decode){return WebUtility.HtmlDecode(html);}
            return html;
        }finally{CultureInfo.CurrentUICulture=culture;}
    }
}
