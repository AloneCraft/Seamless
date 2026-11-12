using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using ResultTable=Seamless.Management.Components.問い合わせ結果表;

namespace Management.Tests;

public sealed class 問い合わせ結果表{
    [Theory]
    [InlineData("[4,5]",false,false,"4|5")]
    [InlineData("[\"alpha\",\"<beta>\"]",true,false,"alpha|<beta>")]
    [InlineData("[4,null,5]",false,true,"4||5")]
    public async Task ScalarSequencesUseOneValueColumn(string json,bool lengthMetadata,bool hasMore,string expected){
        CatalogField[] columns=[];
        if(lengthMetadata){columns=[new("Length","int")];}
        var result=Result(json,columns,hasMore);
        var html=await Render(result);
        Assert.Equal(new[]{"値"},Headers(html));
        Assert.Equal(expected.Split('|'),Cells(html));
        Assert.Equal(result.Rows.Length,Rows(html).Length);
        Assert.Contains(result.Rows.Length+" 行を表示",Text(html),StringComparison.Ordinal);
        var limited=Text(html).Contains("表示上限に達しました",StringComparison.Ordinal);
        Assert.Equal(hasMore,limited);
        if(lengthMetadata){Assert.DoesNotContain("<beta>",html,StringComparison.Ordinal);}
    }

    [Fact]
    public async Task RecordAndNullRowsKeepTheExistingColumnOrder(){
        var result=Result("[{\"Id\":1,\"Name\":\"first\"},null,{\"Id\":2,\"Name\":\"second\"}]",[new("Name","string"),new("Id","int")]);
        var html=await Render(result);
        Assert.Equal(new[]{"Name","Id"},Headers(html));
        Assert.Equal(new[]{"first","1","","","second","2"},Cells(html));
        Assert.Equal(3,Rows(html).Length);
        Assert.Equal(2,Regex.Matches(Rows(html)[1],"<td(?:\\s[^>]*)?>",RegexOptions.Singleline).Count);
    }

    [Fact]
    public async Task HeadersUseDeclaredNamesWhileCellsUseTheJsonKeys(){
        {
            //サーバーが生成型から作る列情報: キーは管理JSONの命名規則、表示名は宣言名
            var columns=Seamless.Management.Server.RuntimeValueJson.Columns(typeof(DeclaredRow));
            Assert.Equal(new (string,string?)[]{("amount","Amount"),("title","Title")},columns.Select(column=>(column.Name,column.DisplayName)));
            var row=Seamless.Management.Server.RuntimeValueJson.Serialize(new DeclaredRow(2m,"row2"));
            Assert.Equal(new[]{"amount","title"},row.EnumerateObject().Select(property=>property.Name));
            Assert.DoesNotContain("displayName",JsonSerializer.Serialize(new CatalogField("Id","int"),ManagementJson.Options),StringComparison.Ordinal);
            var html=await Render(new(2,true,null,[row,Seamless.Management.Server.RuntimeValueJson.Serialize(new DeclaredRow(3m,"row3"))],columns,false));
            Assert.Equal(new[]{"Amount","Title"},Headers(html));
            Assert.Equal(new[]{"2","row2","3","row3"},Cells(html));
        }
        {
            //表示名を持たない旧サーバーの列情報はキーをそのまま見出しにする
            var html=await Render(Result("[{\"amount\":1}]",[new("amount","decimal")]));
            Assert.Equal(new[]{"amount"},Headers(html));
            Assert.Equal(new[]{"1"},Cells(html));
        }
    }
    public readonly record struct DeclaredRow(decimal Amount,string Title);

    [Fact]
    public async Task EmptySequenceWithoutMetadataStillHasAValueColumn(){
        var html=await Render(Result("[]",[]));
        Assert.Equal(new[]{"値"},Headers(html));
        Assert.Empty(Rows(html));
        Assert.Empty(Cells(html));
        Assert.Contains("0 行を表示",Text(html),StringComparison.Ordinal);
        Assert.DoesNotContain("表示上限に達しました",Text(html),StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompositeJsonWithoutMetadataUsesOneCellPerRow(){
        const string json="[{\"Id\":1,\"Name\":\"<value>\"},[1,2],null]";
        var html=await Render(Result(json,[]));
        Assert.Equal(new[]{"値"},Headers(html));
        Assert.Equal(new[]{"{\"Id\":1,\"Name\":\"<value>\"}","[1,2]",""},Cells(html));
        Assert.Equal(3,Rows(html).Length);
        Assert.DoesNotContain("<value>",html,StringComparison.Ordinal);
    }

    private static ObjectExecutionResult Result(string json,CatalogField[] columns,bool hasMore=false){
        using var document=JsonDocument.Parse(json);
        return new(2,true,null,document.RootElement.EnumerateArray().Select(static row=>row.Clone()).ToArray(),columns,hasMore);
    }

    private static async Task<string> Render(ObjectExecutionResult result){
        var previous=CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("ja-JP");
        try{
            using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async()=>{
                var component=await renderer.RenderComponentAsync<ResultTable>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"結果",result}}));
                return component.ToHtmlString();
            });
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    private static string[] Headers(string html)=>Regex.Matches(html,"<th(?:\\s[^>]*)?>(.*?)</th>",RegexOptions.Singleline).Select(match=>Text(match.Groups[1].Value)).ToArray();
    private static string[] Cells(string html)=>Regex.Matches(html,"<td(?:\\s[^>]*)?>(.*?)</td>",RegexOptions.Singleline).Select(match=>Text(match.Groups[1].Value)).ToArray();
    private static string[] Rows(string html)=>Regex.Matches(Regex.Match(html,"<tbody[^>]*>(.*?)</tbody>",RegexOptions.Singleline).Groups[1].Value,"<tr(?:\\s[^>]*)?>.*?</tr>",RegexOptions.Singleline).Select(match=>match.Value).ToArray();
    private static string Text(string html)=>WebUtility.HtmlDecode(Regex.Replace(html,"<[^>]*>","",RegexOptions.Singleline)).Trim();
}
