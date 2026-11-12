using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ResourceHeatPlayback{
    [Fact]public void LineOverlayDoesNotCrossMissingWindowsAndKeepsObservedZero(){
        var trace=Trace();var layout=資源ヒート配置.作成(trace,null);var lines=資源折線配置.作成(layout);
        Assert.Equal(2,lines.区間.Length);Assert.Empty(lines.点);Assert.All(lines.区間,item=>Assert.Equal("Bytes",item.単位));
        Assert.Contains("M210",lines.区間[0].経路);Assert.DoesNotContain("M210",lines.区間[1].経路);
        Assert.Equal(0,lines.区間[0].最小値);Assert.Equal(20,lines.区間[0].最大値);
        var empty=trace with{Samples=[trace.Samples[1]]};Assert.Empty(資源折線配置.作成(資源ヒート配置.作成(empty,null)).区間);
    }
    [Fact]public void InstantAndSingleWindowSamplesHaveIndependentHealthyRepresentations(){
        var trace=Trace();var point=trace with{Samples=[new("memory",0,0,0,null,Aggregation:"Instant")]};
        var lines=資源折線配置.作成(資源ヒート配置.作成(point,null));Assert.Empty(lines.区間);Assert.Equal(0,Assert.Single(lines.点).値);
        var window=trace with{Samples=[trace.Samples[0]]};Assert.Single(資源折線配置.作成(資源ヒート配置.作成(window,null)).区間);
        var unknownPercent=window with{Lanes=[window.Lanes[0] with{PercentBasis=new("Capacity",100,"Bytes","OS")} ]};
        Assert.Empty(資源折線配置.作成(資源ヒート配置.作成(unknownPercent,null)).区間);
    }
    [Fact]public void SelectedTimeClipsTheDisplayAndPreservesOriginalObservationWindows(){
        var trace=Trace();var layout=資源ヒート配置.作成(trace,null,rangeStart:5,rangeEnd:25);
        Assert.Equal(5,layout.開始);Assert.Equal(25,layout.終了);Assert.Equal(3,layout.サンプル.Length);Assert.Equal(0,layout.サンプル[0].値.StartTimestamp);Assert.Equal(250,layout.サンプル[0].幅);
        Assert.Equal("Missing",Assert.Single(資源ヒート配置.作成(trace,null,rangeStart:10,rangeEnd:20).サンプル).値.State);
        Assert.Throws<InvalidDataException>(()=>資源ヒート配置.作成(trace,null,rangeStart:25,rangeEnd:5));
        Assert.Throws<InvalidDataException>(()=>資源ヒート配置.作成(trace,null,rangeStart:-1,rangeEnd:25));
    }
    [Fact]public void PlaybackUsesElapsedTimeAndClockFrequencyWithoutCreatingSamples(){
        Assert.Equal(500,資源折線配置.再生位置(100,0.2,2,0,1000,1000));
        Assert.Equal(1000,資源折線配置.再生位置(0,5,1,0,1000,1000));
        Assert.Equal(1000,資源折線配置.再生位置(0,0,1,0,0,1000));
        Assert.Throws<ArgumentOutOfRangeException>(()=>資源折線配置.再生位置(0,-1,1,0,1000,1000));
        var trace=Trace();Assert.Equal(4,trace.Samples.Length);
    }
    [Fact]public async Task PlaybackStopsWhenHiddenAndImportedStateStartsPaused(){
        var trace=Trace() with{Clock=new("clock",1,0,DateTimeOffset.UnixEpoch)};
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());TestHeat? view=null;
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<TestHeat>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"記録",trace},{"Capture",(Action<TestHeat>)(value=>view=value)},{"初期保存設定",new PlanSnapshotHeatSettings(Cursor:200){Overlay=true,PlaybackSpeed=4,RangeStart=5,RangeEnd=25}}}));
            Assert.False(view!.Playing);Assert.True(view.保存設定.Overlay);Assert.Equal(4,view.保存設定.PlaybackSpeed);Assert.Equal(5,view.保存設定.RangeStart);
            await view.Play();Assert.True(view.Playing);
        });
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(await renderer.Dispatcher.InvokeAsync(()=>view!.保存設定.Cursor<=200)){await Task.Delay(20,timeout.Token);}
        await renderer.Dispatcher.InvokeAsync(async()=>{await view!.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示中か",false}}));await view.Stopped();Assert.False(view.Playing);Assert.True(view.保存設定.Cursor>200);Assert.Equal(4,trace.Samples.Length);});
    }
    public sealed class TestHeat:資源ヒートガント{
        [Parameter]public Action<TestHeat> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public bool Playing=>typeof(資源ヒートガント).GetField("再生取消",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this) is not null;
        public Task Play()=>(Task)typeof(資源ヒートガント).GetMethod("再生切替時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[])!;
        public Task Stopped()=>(Task)typeof(資源ヒートガント).GetMethod("停止Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[])!;
    }
    private static ResourceHeatTrace Trace()=>new(Guid.NewGuid(),"definition","Query",1,new('A',64),true,new("clock",1000,0,DateTimeOffset.UnixEpoch),
        [new("memory","Memory","Memory","Host","UsedMemory","Bytes")],
        [new("memory",0,10,0,null),new("memory",10,20,null,null,"Missing","Unknown",MissingReason:"Unavailable"),new("memory",20,30,10,null),new("memory",30,40,20,null)],TimeSpan.FromMilliseconds(100),0,[]);
}
