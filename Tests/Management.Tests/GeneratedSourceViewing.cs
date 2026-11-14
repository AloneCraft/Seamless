using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;
using Panel=Seamless.Management.Components.生成コード表示;

namespace Management.Tests;

public sealed class GeneratedSourceViewing{
    [Fact]
    public void GenerateSourcesReturnsUserAndGeneratorFilesWithoutRunningUserCode(){
        var definition=Designer.ApplyQuery(Designer.Apply(Designer.Empty("generated-sources","Generated sources"),new("Items","",[new("Id","int",true)],[])),
            new("Summary","",DatabaseQueryKind.View,[new("Id","int",false)],[],"","return q.Items.Select(row=>new Summary(row.Id));"));
        //静的コンストラクターで例外を出す型を足しても、生成ソースの取得は利用者コードを実行しない
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Guard.cs","namespace UserCode;public sealed class Guard{static Guard(){throw new System.InvalidOperationException(\"DO_NOT_RUN\");}}")]}};
        var files=new RuntimeCompiler().GenerateSources(definition.Source);
        Assert.Equal(definition.Source.Files.Select(file=>file.Path),files.Where(file=>file.Kind=="User").Select(file=>file.Path));
        // ビューは IReader への拡張メンバー。DB生成器は目録と、名前で呼ぶ入口(Queries)を作る。
        var queries=Assert.Single(files,file=>file.Kind=="Database"&&file.Path.EndsWith("ManagedDb.Queries.g.cs",StringComparison.Ordinal));
        Assert.Contains("reader.Summary",queries.Text);
        Assert.Contains(files,file=>file.Kind=="Database"&&file.Text.Contains("DatabaseObjectKind.View,typeof(global::UserCode.Views.Summary)",StringComparison.Ordinal));
        Assert.All(files,file=>Assert.DoesNotContain('\\',file.Path));
    }

    [Fact]
    public async Task PanelListsFilesAndShowsUnsupportedNoticesInRed(){
        GeneratedSourceFile[] files=[new("User","Database.cs","namespace UserCode;"),new("Database","UserCode.ManagedDb.Queries.g.cs","// generated queries")];
        var html=await Render(new Dictionary<string,object?>{{"題名","db · 全ソース"},{"ファイル",files}});
        Assert.Contains("db · 全ソース",html);
        Assert.Contains("Database.cs",html);
        Assert.Contains("UserCode.ManagedDb.Queries.g.cs",html);
        Assert.Contains("DB生成器",html);
        Assert.Contains("aria-current=\"true\"",html);
        Assert.Contains("namespace UserCode;",html);
        var notice=await Render(new Dictionary<string,object?>{{"題名","db"},{"案内","このホストは生成コードの表示に対応していません（未対応）。"},{"未対応か",true}});
        Assert.Contains("class=\"未対応通知\"",notice);
        Assert.Contains("表示できるファイルがありません。",notice);
    }

    private static async Task<string> Render(Dictionary<string,object?> parameters){
        var previous=CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("ja-JP");
        try{
            using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async()=>WebUtility.HtmlDecode((await renderer.RenderComponentAsync<Panel>(ParameterView.FromDictionary(parameters))).ToHtmlString()));
        }finally{CultureInfo.CurrentUICulture=previous;}
    }
}
