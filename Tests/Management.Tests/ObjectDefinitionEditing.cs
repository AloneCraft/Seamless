using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class ObjectDefinitionEditing{
    [Fact]public void RecordStructureEditsPreserveUsingNamespacesCommentsMethodsAndOtherObjects(){
        var definition=Declared();var target=Target();
        var baseline=Designer.ReadObjectDefinition(definition,target);
        Assert.Equal(new[]{"Id","Title"},baseline.Columns.Select(column=>column.Name));
        var columns=baseline.Columns.Select(column=>{if(column.Type=="int"){return column with{Type="long"};}return column;}).ToArray();
        var result=Designer.ApplyObjectStructure(definition,baseline,columns,[],baseline.ReturnType);
        Assert.True(result.Succeeded);var text=result.Definition!.Source.Files[0].Text;
        Assert.Contains("using System; // import",text);Assert.Contains("// row documentation",text);Assert.Contains("namespace Tables",text);
        Assert.Contains("public string Describe()=>Title;",text);Assert.Contains("public record struct Other(int Value);",text);Assert.Contains("long Id",text);
        Assert.Equal("Id",Assert.Single(result.Definition.Migration!.Tables).Columns[0].Source);
    }

    [Fact]public void PartialDeclarationsAreSelectableAndStructureChangesStayInTheirOriginalFiles(){
        var definition=Declared() with{Source=new("1",[
            new("Root.cs","namespace Sample{internal static partial class DatabaseGenerator;} namespace Sample.Tables{public partial struct Item{public int Id{get;init;} public string Describe()=>Id.ToString();}}"),
            new("More.cs","// file comment\nnamespace Sample.Tables{public partial struct Item{public string Title{get;init;}}}")
        ],[])};
        var design=Designer.ReadObjectDefinition(definition,Target());Assert.Equal(2,design.Parts.Length);
        var columns=design.Columns.Select(column=>{if(column.Name=="Title"){return column with{Type="string?"};}return column;}).ToArray();
        var result=Designer.ApplyObjectStructure(definition,design,columns,[],design.ReturnType);
        Assert.True(result.Succeeded);Assert.Equal(definition.Source.Files[0].Text,result.Definition!.Source.Files[0].Text);
        Assert.Contains("string? Title",result.Definition.Source.Files[1].Text);Assert.StartsWith("// file comment",result.Definition.Source.Files[1].Text);
    }

    [Fact]public void SourceEditsRebaseOtherTargetsAndRejectConcurrentChangesToTheSameTarget(){
        var definition=Declared();var baseline=Designer.ReadObjectDefinition(definition,Target());var part=Assert.Single(baseline.Parts);
        var replacement=part.Text.Replace("Describe()=>Title","Describe()=>Title.ToUpperInvariant()",StringComparison.Ordinal);
        var other=definition with{Source=definition.Source with{Files=[definition.Source.Files[0] with{Text=definition.Source.Files[0].Text.Replace("Other(int Value)","Other(long Value)",StringComparison.Ordinal)}]}};
        var applied=Designer.ApplyObjectSource(other,baseline,part.Id,replacement);Assert.True(applied.Succeeded);
        Assert.Contains("Other(long Value)",applied.Definition!.Source.Files[0].Text);Assert.Contains("Title.ToUpperInvariant()",applied.Definition.Source.Files[0].Text);
        var conflict=definition with{Source=definition.Source with{Files=[definition.Source.Files[0] with{Text=definition.Source.Files[0].Text.Replace("Describe()=>Title","Describe()=>Title.Trim()",StringComparison.Ordinal)}]}};
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Designer.ApplyObjectSource(conflict,baseline,part.Id,replacement)).Code);
    }

    [Fact]public void InvalidCSharpReturnsDiagnosticsAndLeavesTheDefinitionUntouched(){
        var definition=Declared();var baseline=Designer.ReadObjectDefinition(definition,Target());var part=Assert.Single(baseline.Parts);
        var result=Designer.ApplyObjectSource(definition,baseline,part.Id,"public record struct Item(");
        Assert.False(result.Succeeded);Assert.Null(result.Definition);Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(Declared().Source.Files[0].Text,definition.Source.Files[0].Text);
    }

    [Fact]public void RegisteredQueryUsesItsEntryRatherThanTheSyntheticTreeIdentity(){
        var definition=new Definition("registered-id","DB",DefinitionKind.Query,"Queries.Api","Run",new("1",[new("Query.cs","namespace Queries{public record struct Result(int Id);public static class Api{public static Result Run(int id){return new(id);}}}")],[]));
        var target=new CatalogObject("registered:registered-id","registered-id","","RegisteredFunction","Queries.Result");
        var design=Designer.ReadObjectDefinition(definition,target);Assert.Contains(design.Parts,part=>part.Role=="Record");Assert.Contains(design.Parts,part=>part.Role=="Member");
        var result=Designer.ApplyObjectStructure(definition,design,design.Columns,[new("id","long")],design.ReturnType);
        Assert.True(result.Succeeded);Assert.Contains("Run(long id)",result.Definition!.Source.Files[0].Text);
    }

    [Fact]public async Task InvalidSourceTabSyncRetainsInputAndDoesNotEmitAnUpdatedDefinition(){
        var updates=new List<Definition>();var dirty=new List<bool>();var definition=Declared();
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestEditor? editor=null;
            await renderer.RenderComponentAsync<TestEditor>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"定義",definition},{"対象",Target()},{"表示モード","Source"},{"Capture",(Action<TestEditor>)(value=>editor=value)},
                {"定義変更時",EventCallback.Factory.Create<Definition>(updates,(Definition value)=>updates.Add(value))},{"未保存変更時",EventCallback.Factory.Create<bool>(dirty,(bool value)=>dirty.Add(value))}
            }));
            await editor!.Change("public record struct Item(");Assert.False(await editor.同期Async());
            Assert.Equal("public record struct Item(",editor.Code);Assert.Empty(updates);Assert.True(dirty[^1]);
        });
    }
    [Fact]public async Task ScalarColumnCommandKeepsTheDefinitionAndExplainsItsEditableShape(){
        var definition=Designer.Empty("db","DB");definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"));
        var updates=new List<Definition>();var dirty=new List<bool>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestEditor? editor=null;
            var rendered=await renderer.RenderComponentAsync<TestEditor>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"定義",definition},{"対象",Designer.Objects(definition).Single(item=>item.Name=="Amount")},{"Capture",(Action<TestEditor>)(value=>editor=value)},
                {"定義変更時",EventCallback.Factory.Create<Definition>(updates,(Definition value)=>updates.Add(value))},{"未保存変更時",EventCallback.Factory.Create<bool>(dirty,(bool value)=>dirty.Add(value))}
            }));
            await editor!.列を追加Async();Assert.Empty(updates);Assert.Empty(dirty);
            Assert.Contains("スカラー関数には結果列がありません。引数と戻り型を編集してください。",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
        });
    }
    // ビュー・関数の行の型は、表の列と同じく画面で足し・名前と型を変えられる(I11)。推論の時代の読み取り専用は無い。
    [Fact]public async Task ViewRowTypeColumnsAreEditableLikeTableColumns(){
        var definition=Designer.Empty("db","DB");definition=Designer.ApplyQuery(definition,new("Recent","",DatabaseQueryKind.View,[new("Id","int",false)],[],"","throw new System.NotImplementedException();"));
        var target=Designer.Objects(definition).Single(item=>item.Name=="Recent");
        var design=Designer.ReadObjectDefinition(definition,target);Assert.True(design.StructureAvailable,design.StructureUnavailableReason);
        var edit=Designer.ApplyObjectStructure(definition,design,[new("Id","long",false,"Id"),new("Title","string",false)],[],design.ReturnType);
        Assert.True(edit.Succeeded,string.Join(Environment.NewLine,edit.Diagnostics.Select(item=>item.Message)));
        Assert.Equal(new[]{("Id","long"),("Title","string")},Designer.ReadObjectDefinition(edit.Definition!,target).Columns.Select(column=>(column.Name,column.Type)));
        var updates=new List<Definition>();var dirty=new List<bool>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestEditor? editor=null;
            var rendered=await renderer.RenderComponentAsync<TestEditor>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"定義",definition},{"対象",target},{"Capture",(Action<TestEditor>)(value=>editor=value)},
                {"定義変更時",EventCallback.Factory.Create<Definition>(updates,(Definition value)=>updates.Add(value))},{"未保存変更時",EventCallback.Factory.Create<bool>(dirty,(bool value)=>dirty.Add(value))}
            }));
            var html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.DoesNotContain("readonly",html);Assert.Contains("aria-label=\"結果列の型\"",html);Assert.Contains("列を追加",html);
            await editor!.列を追加Async();Assert.True(dirty[^1]);Assert.True(await editor.同期Async());
            Assert.Contains(Designer.ReadObjectDefinition(updates[^1],target).Columns,column=>column.Name=="Column");
        });
    }
    // 関数の戻り型も画面で変えられる(処理本体からの推論は無い)。
    [Fact]public void FunctionReturnTypeIsEditedThroughTheStructure(){
        var definition=Designer.Empty("db","DB");definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"));
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");var design=Designer.ReadObjectDefinition(definition,target);
        var edit=Designer.ApplyObjectStructure(definition,design,design.Columns,design.Parameters,"long");
        Assert.True(edit.Succeeded,string.Join(Environment.NewLine,edit.Diagnostics.Select(item=>item.Message)));
        Assert.Equal("long",Designer.ReadObjectDefinition(edit.Definition!,target).ReturnType);
    }
    [Fact]public async Task SourceTabAlignsLegacyFunctionWithItsFullFileAndKeepsItsEditRange(){
        var definition=Designer.Empty("db","DB");
        definition=Designer.ApplyQuery(definition,new("F","",DatabaseQueryKind.ScalarFunction,[],[new("n","int")],"int","return n;"));
        const string path="Queries/F.cs";
        var original=definition.Source.Files.Single(file=>file.Path==path).Text;
        var legacy=original.Replace("            public", "public",StringComparison.Ordinal)
            .Replace("                return", "return",StringComparison.Ordinal).Replace("            }", "}",StringComparison.Ordinal);
        Assert.NotEqual(original,legacy);
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file.Path==path?file with{Text=legacy}:file).ToArray()}};
        var target=Designer.Objects(definition).Single(item=>item.Name=="F");
        var updates=new List<Definition>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestEditor? editor=null;
            await renderer.RenderComponentAsync<TestEditor>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"定義",definition},{"対象",target},{"表示モード","Source"},{"Capture",(Action<TestEditor>)(value=>editor=value)},
                {"定義変更時",EventCallback.Factory.Create<Definition>(updates,(Definition value)=>updates.Add(value))}
            }));
            var originalPart=Designer.ReadObjectDefinition(definition,target).Parts.Single(part=>part.Role=="Member");
            Assert.StartsWith("            public int @F(int @n){\n                return n;\n            }",editor!.Code);
            Assert.Empty(updates);
            Assert.Equal(legacy,definition.Source.Files.Single(file=>file.Path==path).Text);
            var replacement=editor.Code.Replace("return n;","return n+1;",StringComparison.Ordinal);
            await editor.Change(replacement);
            Assert.True(await editor.同期Async());
            var edited=Assert.Single(updates).Source.Files.Single(file=>file.Path==path).Text;
            Assert.Equal(legacy.Remove(originalPart.Start,originalPart.Length).Insert(originalPart.Start,replacement),edited);
        });
    }
    public sealed class TestEditor:対象定義エディター{
        [Parameter]public Action<TestEditor> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public string Code=>(string)typeof(対象定義エディター).GetField("入力コード",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(this)!;
        public Task Change(string value)=>(Task)typeof(対象定義エディター).GetMethod("コード変更時Async",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(this,[value])!;
    }
    private static void AssertSdkCompilation(SourceSnapshot source){
        var options=new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14);
        var trees=source.Files.Select(file=>Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(file.Text,options,file.Path));
        var compilation=Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("QueryPreservation_"+Guid.NewGuid().ToString("N"),trees,global::Seamless.Management.SourceEditor.PlatformReferences(),new(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:Microsoft.CodeAnalysis.NullableContextOptions.Enable));
        Microsoft.CodeAnalysis.GeneratorDriver driver=Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create([Microsoft.CodeAnalysis.GeneratorExtensions.AsSourceGenerator(new Seamless.Database.Generator.DatabaseSchemaGenerator()),Microsoft.CodeAnalysis.GeneratorExtensions.AsSourceGenerator(new Seamless.Database.Generator.DatabaseContainerGenerator())],parseOptions:options);
        driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);Assert.DoesNotContain(diagnostics,item=>item.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics));
    }
    private static CatalogObject Target()=>new("Item","Item","","Table","global::Sample.Tables.Item");
    private static Definition Declared()=>new("db","DB",DefinitionKind.Database,"Sample.Container","",new("1",[new("Definition.cs","using System; // import\nnamespace Sample{internal static partial class DatabaseGenerator;namespace Tables{\n// row documentation\npublic partial record struct Item(int Id,string Title){public string Describe()=>Title;}\npublic record struct Other(int Value);}}")],[]));
}
