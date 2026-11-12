using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Theory,InlineData("ScalarFunction"),InlineData("TableFunction"),InlineData("View")]
    public async Task ResultTypesAreReadOnlyWhileParametersRemainEditable(string kind){
        var definition=FunctionResultFixture(kind=="TableFunction");
        var name="Amount";
        if(kind=="View"){
            name="Summary";
            definition=Designer.ApplyQuery(definition,new(name,"",DatabaseQueryKind.View,[new("Value","int",false)],[],"","return [];"));
        }
        var target=Designer.Objects(definition).Single(item=>item.Name==name);
        var html=await RenderFunctionResultEditor(definition,target);
        var label="戻り型";if(kind!="ScalarFunction"){label="結果列の型";}
        var input=Regex.Match(html,"<input(?=[^>]*aria-label=\""+label+"\")[^>]*>").Value;
        Assert.NotEmpty(input);Assert.Contains("readonly",input);
        if(kind!="View"){Assert.Contains("aria-label=\"引数の型\"",html);}
        else{Assert.Contains("型推論通知",html);Assert.Contains("このビューはC#宣言の列を使用しています。",html);}
    }

    [Fact]
    public async Task InferredViewShowsColumnsWithoutARestrictionNotice(){
        var definition=Designer.ApplyQuery(Designer.Empty("view-ui","View UI"),
            new("Summary","",DatabaseQueryKind.View,[],[],"","return new[]{new{Total=3m}};"));
        var target=Assert.Single(Designer.Objects(definition));
        var html=await RenderFunctionResultEditor(definition,target);
        Assert.Contains("value=\"Total\"",html);
        Assert.Contains("value=\"decimal\"",html);
        Assert.DoesNotContain("型推論通知",html);
        Assert.DoesNotContain("列を追加",html);
    }

    [Theory,InlineData("ja-JP"),InlineData("en-US")]
    public async Task TargetTypedFunctionsExplainTheDeclaredTypeFallback(string language){
        var definition=FunctionResultFixture(false);
        var query=Designer.ReadQuery(definition,"Amount");
        definition=Designer.ApplyQuery(definition,query with{Body="return default;"});
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        Assert.Equal("DeclaredResultTypeRequired",Designer.ReadObjectDefinition(definition,target).ResultTypeInferenceReason);
        var html=await RenderFunctionResultEditor(definition,target,language);
        Assert.Contains("型推論通知",html);
        if(language=="en-US"){Assert.Contains("This body requires a declared return type",html);}
        else{Assert.Contains("この処理本体だけでは戻り型を決められません。",html);}
    }

    private static async Task<string> RenderFunctionResultEditor(Definition definition,CatalogObject target,string language="ja-JP"){
        var before=CultureInfo.CurrentUICulture;CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(language);
        try{
            using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async()=>{
                var component=await renderer.RenderComponentAsync<対象定義エディター>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"定義",definition},{"対象",target}}));
                return WebUtility.HtmlDecode(component.ToHtmlString());
            });
        }finally{CultureInfo.CurrentUICulture=before;}
    }
}
