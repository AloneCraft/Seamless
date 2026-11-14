using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management.Components;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagerPresentation{
    [Theory]
    [InlineData("{0} 行","12 rows")]
    [InlineData("元に戻す ({0})","Undo (12)")]
    [InlineData("変更を保存 ({0})","Save changes (12)")]
    [InlineData("操作イベント · {0} 件","Operation events · 12 entries")]
    [InlineData("公開版 v{0} の問い合わせを実行します。","Run the query from published version v12.")]
    public void DynamicDetailsUseTheCurrentLanguage(string template,string expected){
        var previous=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("en");
            Assert.Equal(expected,表示文言.取得(template,12));
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("ja");
            Assert.Equal(string.Format(template,12),表示文言.取得(template,12));
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    [Fact]
    public async Task EnglishQueryAndGridRenderTheirDynamicLabelsWithoutChangingObjectNames(){
        var previous=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("en");
            var operations=new ResultsGridOperations.GridOperations{
                Read=(_,_)=>Task.FromResult(new Seamless.Management.TableRows(1,[],false,[new("Id","int")]))
            };
            using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().AddSingleton<Seamless.Management.IManagementOperations>(operations).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
            await renderer.Dispatcher.InvokeAsync(async()=>{
                var grid=await renderer.RenderComponentAsync<結果グリッド>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","Sample"},{"テーブル名","Items"}}));
                var gridHtml=WebUtility.HtmlDecode(grid.ToHtmlString());
                Assert.Contains("0 rows",gridHtml);Assert.Contains("Items",gridHtml);Assert.DoesNotContain("0 行",gridHtml);
                var values=Seamless.Management.ManagementJson.Element(new Dictionary<string,int>{{"Id",1}});
                operations.Read=(_,_)=>Task.FromResult(new Seamless.Management.TableRows(1,[new(values,values,new string('C',64))],false,[new("Id","int")]));
                var populated=await renderer.RenderComponentAsync<結果グリッド>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","Sample"},{"テーブル名","Items"}}));
                Assert.Contains("aria-label=\"Row 1 Id\"",WebUtility.HtmlDecode(populated.ToHtmlString()));
                //値のキーと宣言名が異なる列は、見出しと読み上げに宣言名、値にはキーを使う
                var keyed=Seamless.Management.ManagementJson.Element(new Dictionary<string,int>{{"amount",7}});
                operations.Read=(_,_)=>Task.FromResult(new Seamless.Management.TableRows(1,[new(keyed,keyed,new string('C',64))],false,[new("amount","int"){DisplayName="Amount"}]));
                var declared=WebUtility.HtmlDecode((await renderer.RenderComponentAsync<結果グリッド>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","Sample"},{"テーブル名","Items"}}))).ToHtmlString());
                Assert.Contains("<span>Amount</span>",declared);Assert.Contains("aria-label=\"Row 1 Amount\"",declared);Assert.Contains("value=\"7\"",declared);
                Assert.DoesNotContain("<span>amount</span>",declared);
                Assert.Equal("New row Id",表示文言.取得("新規行 {0}","Id"));
                Assert.Equal("Query.cs · Start 3 · Length 5",表示文言.取得("{0} · 開始 {1} · 長さ {2}","Query.cs",3,5));
                var query=new Seamless.Management.QueryDesign("FindItems","dbo",Seamless.Management.DatabaseQueryKind.TableFunction,[],[new("minPrice","decimal")],"ISet<Item>","");
                var result=await renderer.RenderComponentAsync<問い合わせ結果>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","Sample"},{"定義",query},{"公開版",12L}}));
                var queryHtml=WebUtility.HtmlDecode(result.ToHtmlString());
                Assert.Contains("Run the query from published version v12.",queryHtml);
                Assert.Contains("Argument minPrice",queryHtml);Assert.Contains("dbo.FindItems",queryHtml);
                Assert.DoesNotContain("公開版",queryHtml);
            });
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    [Fact]
    public void EnglishSatelliteContainsEveryNeutralResource(){
        var resources=new System.Resources.ResourceManager(typeof(表示文言));
        using var neutral=resources.GetResourceSet(CultureInfo.InvariantCulture,true,false)!;
        using var english=resources.GetResourceSet(CultureInfo.GetCultureInfo("en"),true,false)!;
        Assert.NotNull(neutral);Assert.NotNull(english);
        var count=0;
        foreach(System.Collections.DictionaryEntry entry in neutral){
            count++;
            Assert.False(string.IsNullOrWhiteSpace(english.GetString((string)entry.Key)));
        }
        Assert.True(count>400);
    }

    [Theory]
    [InlineData("ja-JP","変更を確認","保存","参照")]
    [InlineData("en-US","Review changes","Save","Read")]
    [InlineData("en-GB","Review changes","Save","Read")]
    public void ResourcesFollowTheCurrentUiCultureAndPreserveUserData(string culture,string review,string save,string read){
        var previous=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(culture);
            Assert.Equal(review,表示文言.取得("変更を確認"));
            Assert.Equal(save,表示文言.取得("保存"));
            Assert.Equal(read,表示文言.文脈("権限","参照"));
            Assert.Equal("global::Sample.Tables.Item",表示文言.取得("global::Sample.Tables.Item"));
            Assert.Contains("MyDatabase",表示文言.取得("{0}に追加","MyDatabase"));
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    [Theory]
    [InlineData("変更を確認")]
    [InlineData("保存")]
    [InlineData("削除")]
    public async Task IconsUseDecorativeCurrentColorVectors(string action){
        using var provider=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<操作アイコン>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"種類",action}}));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });
        Assert.Contains("<svg",html);Assert.Contains("<path",html);
        Assert.Contains("stroke=\"currentColor\"",html);
        Assert.Contains("aria-hidden=\"true\"",html);
        Assert.Contains("focusable=\"false\"",html);
        Assert.DoesNotContain("<text",html);
    }

    [Fact]
    public async Task EnglishMenusAndSearchUseTheSameCommandsAndKeepTheirIcons(){
        var previous=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("en");
            var commands=new コマンド登録<string>([
                new("review","変更を確認",_=>true,_=>true,_=>Task.CompletedTask,[]),
                new("delete","削除",_=>true,_=>false,_=>Task.CompletedTask,[],破壊的か:true),
                new("find","Find",_=>true,_=>true,_=>Task.CompletedTask,[],アイコン:"検索")
            ]);
            Assert.Equal("review",Assert.Single(commands.検索("db","review")).ID);
            using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoScript>().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
            var html=await renderer.Dispatcher.InvokeAsync(async()=>{
                var rendered=await renderer.RenderComponentAsync<操作メニュー<string>>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"登録",commands},{"対象","db"}}));
                return WebUtility.HtmlDecode(rendered.ToHtmlString());
            });
            Assert.Contains("Review changes",html);Assert.Contains("Delete",html);
            Assert.Contains("Search actions",html);
            Assert.Equal(3,System.Text.RegularExpressions.Regex.Matches(html,"<svg").Count);
            Assert.Contains("data-操作アイコン=\"検索\"",html);
            Assert.Contains("data-破壊的=\"true\"",html);Assert.Contains("disabled",html);
            Assert.DoesNotContain(">変更を確認<",html);
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    [Fact]
    public async Task BrowserLanguageSelectsEachAuthenticatedPageWithoutAffectingOtherSessions(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"presentation-"+Guid.NewGuid().ToString("N"));
        using var host=InProcessManagementHost.Create(root);
        await using var web=await ManagerWebHost.StartAsync(host);
        async Task Verify(string? language,string expected,string workspace){
            using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
            if(language is not null){client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);}
            var launch=new Uri(web.Sessions.CreateLaunch().Url);
            using(var response=await client.GetAsync(new Uri(launch.GetLeftPart(UriPartial.Path)))){
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                Assert.Equal(expected,Assert.Single(response.Content.Headers.ContentLanguage));
                Assert.Contains("lang=\""+expected+"\"",await response.Content.ReadAsStringAsync());
            }
            var url=new Uri(web.Origin,web.Sessions.PathBase);
            using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(url,"session"));
            request.Headers.Add("Origin",web.Origin.GetLeftPart(UriPartial.Authority));
            request.Content=new StringContent(launch.Fragment[1..],Encoding.UTF8,"text/plain");
            using(var response=await client.SendAsync(request)){Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);}
            using var page=await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK,page.StatusCode);
            var html=WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
            Assert.Contains("<html lang=\""+expected+"\"",html);
            Assert.Contains(workspace,html);
            Assert.Contains("<svg",html);
        }
        await Task.WhenAll(Verify("ja-JP, en;q=0.8","ja","ワークスペース"),Verify("en-GB, ja;q=0.7","en","Workspace"));
        await Verify("fr-FR, en-US;q=0.8, ja;q=0.4","en","Workspace");
        await Verify("fr-FR, de-DE;q=0.9, es-ES;q=0.8, ja-JP;q=0.7","ja","ワークスペース");
        await Verify("fr-FR, ja;q=0","en","Workspace");
        await Verify("fr-FR","en","Workspace");
        await Verify(null,"en","Workspace");
    }

    private sealed class NoScript:IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Static rendering does not call JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
    }
}
