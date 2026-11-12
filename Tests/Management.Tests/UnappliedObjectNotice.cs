using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;
using DocumentView=Seamless.Management.Components.対象文書ビュー;

namespace Management.Tests.UnappliedObjectNotice;

public sealed class 対象文書ビュー{
    [Theory]
    [InlineData("Table","ja-JP")]
    [InlineData("View","ja-JP")]
    [InlineData("ScalarFunction","ja-JP")]
    [InlineData("Table","en-US")]
    [InlineData("View","en-US")]
    [InlineData("ScalarFunction","en-US")]
    public async Task UnappliedDataShowsTheSameNeutralNextStep(string kind,string language){
        var previous=CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(language);
        try{
            var definition=Designer.Empty("draft","DraftDatabase");
            var state=new 対象文書状態("db:draft",definition.Id,new("Pending","Pending","",kind,"int")){データを開いたか=true,表示="データ"};
            using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var html=await renderer.Dispatcher.InvokeAsync(async()=>{
                var root=await renderer.RenderComponentAsync<DocumentView>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                    [nameof(DocumentView.状態)]=state,[nameof(DocumentView.定義)]=definition,
                    [nameof(DocumentView.DB名)]="DraftDatabase",[nameof(DocumentView.公開済みか)]=false
                }));
                return WebUtility.HtmlDecode(root.ToHtmlString());
            });
            if(language=="en-US"){
                Assert.Contains("This definition is in the draft. Review the changes and apply them to the running database.",html);
                Assert.DoesNotContain("view and edit its data",html);
            }else{
                Assert.Contains("この定義は下書きにあります。変更を確認して稼働DBへ適用してください。",html);
                Assert.DoesNotContain("データを表示・編集できます",html);
            }
        }finally{CultureInfo.CurrentUICulture=previous;}
    }
}
