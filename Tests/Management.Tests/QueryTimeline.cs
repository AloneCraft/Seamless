using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Timeline=Seamless.Management.Components.実績タイムライン;

namespace Management.Tests;

public sealed class QueryTimeline{
    [Fact]public async Task TimelineUsesOnlyObservedBoundariesAndSwitchesProjectionWithoutAnExecutionService(){
        var start=new DateTimeOffset(2026,11,9,0,0,0,TimeSpan.Zero);
        var statistics=new ObjectExecutionStatistics(Guid.NewGuid(),"definition","Value",1,new('A',64),start,start.AddMilliseconds(1),TimeSpan.FromMilliseconds(1),TimeSpan.FromTicks(100),TimeSpan.FromTicks(7000),2,2,false);
        using var provider=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTimeline? view=null;
            var rendered=await renderer.RenderComponentAsync<TestTimeline>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"実測",statistics},{"Capture",(Action<TestTimeline>)(value=>view=value)}}));
            var html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.Equal(4,Regex.Matches(html,"<circle").Count);Assert.Equal(2,Regex.Matches(html,"<rect").Count);
            Assert.Contains("00:00:00.000000",html);Assert.DoesNotContain("100%",Regex.Replace(html,"<[^>]+>",""));
            view!.EventOrder();
            html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.Equal(4,Regex.Matches(html,"<circle").Count);Assert.DoesNotContain("<rect",html);Assert.Contains("00:00:00.000700",html);
        });
    }
    public sealed class TestTimeline:Timeline{
        [Parameter]public Action<TestTimeline> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public void EventOrder(){typeof(Timeline).GetField("イベント順か",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,true);this.StateHasChanged();}
    }
}
