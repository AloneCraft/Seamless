using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class CodeCompletionPresentation{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletionOnlyChangesTheSelectedDeclarationAndRejectsStaleText(bool stale){
        const string input="void M(){string text=\"\";text.Sub;}";
        const string before="class C { // preserved\n";
        var source=new SourceSnapshot("r1",[new("Code.cs",before+input+" int Other=42;}" )],[]);
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            Editor? editor=null;string? changed=null;
            var root=await renderer.RenderComponentAsync<Editor>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Capture",(Action<Editor>)(value=>editor=value)},{"値",input},{"補完範囲",new コード補完範囲(source,"Code.cs",before.Length,input.Length)},
                {"値変更時",EventCallback.Factory.Create<string>(new object(),(Action<string>)(value=>changed=value))}
            }));
            await editor!.補完要求Async(input.IndexOf("Sub",StringComparison.Ordinal)+3,input);
            var html=WebUtility.HtmlDecode(root.ToHtmlString());Assert.Contains("C#補完候補",html);Assert.Contains("Substring",html);Assert.Contains("Ctrl+Space",html);
            var result=(CodeCompletionResult)typeof(コードエディター).GetField("補完結果",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editor)!;
            var index=Array.FindIndex(result.Items,item=>item.InsertText=="Substring");Assert.True(index>=0);
            var applied=await editor.補完適用Async(index,stale?input+" ":input);
            if(stale){Assert.Null(applied);Assert.Null(changed);Assert.Contains("入力が変わった",WebUtility.HtmlDecode(root.ToHtmlString()));}
            else{
                Assert.NotNull(applied);Assert.Null(changed);
                Assert.Equal(input.Replace(".Sub;",".Substring;",StringComparison.Ordinal),applied.Text);
                await(Task)typeof(コードエディター).GetMethod("入力時Async",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,[applied.Text])!;
                Assert.Equal(applied.Text,changed);
            }
            Assert.Equal(before+input+" int Other=42;}",source.Files[0].Text);
        });
    }

    [Fact]
    public async Task ReadOnlyEditorsDoNotExposeCompletionControls(){
        var source=new SourceSnapshot("r1",[new("Code.cs","class C {}")],[]);
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<コードエディター>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"値",source.Files[0].Text},{"読み取り専用か",true},{"補完範囲",new コード補完範囲(source,"Code.cs",0,10)}}));return root.ToHtmlString();
        });
        Assert.DoesNotContain("data-code-completion",html);
        Assert.DoesNotContain("Ctrl+Space",html);
    }
    public sealed class Editor:コードエディター{
        [Parameter]public Action<Editor> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    private sealed class NoScripts:IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>throw new NotSupportedException();
    }
}
