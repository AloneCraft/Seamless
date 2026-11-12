using Seamless.Management;
using Seamless.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Reflection;
using System.Net;

namespace Management.Tests;

public sealed class ObjectDisplayOrder{
    private static CatalogObject[] Items()=>[
        new("Items","Items","","Table","Row"),new("Orders","Orders","","Table","Order"),
        new("ItemList","ItemList","","View","ViewRow"),new("OrderList","OrderList","","View","OrderView"),
        new("Amount","Amount","","ScalarFunction","decimal"),new("Find","Find","","TableFunction","Row"),new("registered:query","query","","RegisteredFunction","int"),
        new("dbo0.Items","Items","dbo0","Table","Row"),new("dbo0.Orders","Orders","dbo0","Table","Order")];
    [Theory][InlineData("Table","Items","Orders")][InlineData("View","ItemList","OrderList")][InlineData("Function","Amount","Find")]
    public void MovesOnlyWithinTheSameMembershipAndClassification(string kind,string first,string second){
        var original=Items();var before=original.ToArray();var order=new オブジェクト表示順序();Assert.True(order.移動(original,first,1));
        Assert.Equal(second,order.適用(original,"",kind)[0].Id);Assert.Equal(before,original);
        Assert.Equal(new[]{"dbo0.Items","dbo0.Orders"},order.適用(original,"dbo0","Table").Select(item=>item.Id));
        var otherDb=new オブジェクト表示順序();Assert.Equal(first,otherDb.適用(original,"",kind)[0].Id);
    }
    [Fact]public void DragAndMenuMovesUseTheSameOrderAndRejectForeignOrDeletedTargets(){
        var items=Items();var order=new オブジェクト表示順序();Assert.True(order.対象位置へ移動(items,"","Function","registered:query","Amount",true));
        Assert.Equal(new[]{"registered:query","Amount","Find"},order.適用(items,"","Function").Select(item=>item.Id));
        Assert.False(order.対象位置へ移動(items,"","Table","Items","dbo0.Items",true));Assert.False(order.対象位置へ移動(items,"","View","Items","ItemList",true));
        Assert.False(order.対象位置へ移動(items.Where(item=>item.Id!="Amount"),"","Function","registered:query","Amount",true));
        Assert.True(order.移動(items,"registered:query",1));Assert.Equal("Amount",order.適用(items,"","Function")[0].Id);
    }
    [Fact]public void ReloadAndExplicitResetDoNotRestoreSelfDeclaredOrNameBasedIdentities(){
        var items=Items();var state=new ツリー展開状態();Assert.True(state.表示順序.移動(items,"Items",1));Assert.True(state.表示順序.変更ありか);
        var reload=new ツリー展開状態();Assert.False(reload.表示順序.変更ありか);Assert.Equal("Items",reload.表示順序.適用(items,"","Table")[0].Id);
        state.表示順序.既定順に戻す(items,"Items");Assert.False(state.表示順序.変更ありか);
        Assert.False(state.表示順序.移動(items,"missing",1));Assert.False(state.表示順序.移動(items,"Items",-1));Assert.False(state.表示順序.移動(items,"Items",2));
    }
    [Fact]public async Task CatalogChangesRejectTheOldDragGenerationAndKeepTableChildrenWithTheirRow(){
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());DBオブジェクトツリー? tree=null;
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string,object?>{{nameof(Host.Capture),(Action<DBオブジェクトツリー>)(value=>tree=value)}}));
            var generation=(string)typeof(DBオブジェクトツリー).GetField("表示順世代",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(tree)!;
            await tree!.表示順をドロップAsync("","Table",generation,"Items","Orders",false);
            var html=WebUtility.HtmlDecode(root.ToHtmlString());Assert.True(html.IndexOf("data-対象-id=\"table:Orders\"",StringComparison.Ordinal)<html.IndexOf("data-対象-id=\"table:Items\"",StringComparison.Ordinal));
            Assert.Contains("class=\"所属並び項目\"",html);Assert.Contains("draggable=\"true\"",html);
            await tree.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示版","catalog:2"}}));
            await tree.表示順をドロップAsync("","Table",generation,"Items","Orders",false);
            html=root.ToHtmlString();Assert.True(html.IndexOf("data-対象-id=\"table:Items\"",StringComparison.Ordinal)<html.IndexOf("data-対象-id=\"table:Orders\"",StringComparison.Ordinal));
        });
    }
    [Fact]public async Task LosingViewCapabilityDisablesHandlesAndRejectsLateMoves(){
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());DBオブジェクトツリー? tree=null;
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string,object?>{{nameof(Host.Capture),(Action<DBオブジェクトツリー>)(value=>tree=value)}}));
            await tree!.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示順可能か",false}}));
            Assert.False(tree.表示順を変更できるか("Items",1));await tree.表示順を変更Async("Items",1);
            var html=root.ToHtmlString();Assert.Contains("draggable=\"false\"",html);Assert.True(html.IndexOf("data-対象-id=\"table:Items\"",StringComparison.Ordinal)<html.IndexOf("data-対象-id=\"table:Orders\"",StringComparison.Ordinal));
        });
    }
    private sealed class Host:ComponentBase{
        [Parameter]public Action<DBオブジェクトツリー>? Capture{get;set;}
        private readonly ツリー展開状態 state=new();
        protected override void BuildRenderTree(RenderTreeBuilder builder){
            builder.OpenComponent<DBオブジェクトツリー>(0);builder.AddAttribute(1,"DBID","db:first");builder.AddAttribute(2,"表示版","catalog:1");builder.AddAttribute(3,"表示順可能か",true);builder.AddAttribute(4,"展開状態",this.state);builder.AddAttribute(5,"オブジェクト",Items());builder.AddComponentReferenceCapture(6,value=>this.Capture?.Invoke((DBオブジェクトツリー)value));builder.CloseComponent();
        }
    }
    private sealed class NoScripts:IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Static rendering must not invoke JS.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
    }
}
