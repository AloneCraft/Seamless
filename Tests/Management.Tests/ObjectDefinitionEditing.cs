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
    [Theory][InlineData(false)][InlineData(true)]public void FunctionKindChangesKeepTheExistingDeclarationEnvelope(bool originallySequence){
        var kind=DatabaseQueryKind.ScalarFunction;var helper="ScalarFunction";ColumnDesign[] columns=[];var type="int";var body="return id;";
        if(originallySequence){kind=DatabaseQueryKind.TableFunction;helper="TableFunction";columns=[new("Id","int",false)];type="IEnumerable<OldResult>";body="return [];";}
        var definition=Designer.ApplyQuery(Designer.Empty("kind-change","Kind change"),new("Change","",kind,columns,[new("id","int")],type,body));
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>{if(file.Path=="Database.cs"){return file with{Text=file.Text.Replace("public global::Seamless.Database."+helper,"[global::System.Diagnostics.DebuggerStepThrough] public global::Seamless.Database."+helper,StringComparison.Ordinal).Replace("int @id","[global::System.ComponentModel.Description(\"argument\")] int @id",StringComparison.Ordinal).Replace(")=>this."+helper,")=>/* wrapper comment */this."+helper,StringComparison.Ordinal).Replace(helper+"<",helper+"</* type argument */",StringComparison.Ordinal)};}return file;}).ToArray()}};
        var query=Designer.ReadQuery(definition,"Change");var next=DatabaseQueryKind.TableFunction;columns=[new("Id","int",false)];type="IEnumerable<NewResult>";body="/* new body */return [];";
        if(originallySequence){next=DatabaseQueryKind.ScalarFunction;columns=[];type="int";body="/* new body */return id;";}
        var updated=Designer.ApplyQuery(definition,query with{Kind=next,Columns=columns,ReturnType=type,Body=body});var source=updated.Source.Files.Single(file=>file.Path=="Database.cs").Text;Assert.Contains("DebuggerStepThrough",source);Assert.Contains("Description(\"argument\")",source);Assert.Contains("/* wrapper comment */",source);Assert.Contains("/* new body */",source);
        Assert.Collection(System.Text.RegularExpressions.Regex.Matches(source,@"/\* type argument \*/").Cast<System.Text.RegularExpressions.Match>(),match=>Assert.True(match.Success),match=>Assert.True(match.Success));AssertSdkCompilation(updated.Source);Assert.Equal(next,Designer.ReadQuery(updated,"Change").Kind);
    }
    [Theory][InlineData("View",false)][InlineData("View",true)][InlineData("ScalarFunction",false)][InlineData("ScalarFunction",true)][InlineData("TableFunction",false)][InlineData("TableFunction",true)]
    public void QueryFormStructureEditsKeepAttributesWrapperCommentsAndOtherMembers(string kind,bool changeBody){
        var definition=Designer.Empty("preservation","Preservation");
        const string other="namespace Extras{public static class Other{public const int Value=37;}}";
        var files=definition.Source.Files.ToList();files.Add(new("Unrelated.cs",other));
        var lambda="/* body before */ id+1 /* body after */";var member="[global::System.Diagnostics.DebuggerStepThrough] public global::Seamless.Database.ScalarFunction<int> Amount([global::System.ComponentModel.Description(\"argument\")] int id)=>/* wrapper comment */this.ScalarFunction<int>(()=>"+lambda+");";
        var name="Amount";
        if(kind!="ScalarFunction"){
            var ns="UserCode.Functions";name="Find";if(kind=="View"){ns="UserCode.Views";name="ItemList";}
            var record="[global::System.Serializable] public readonly partial record struct Result([property:global::System.ComponentModel.Description(\"column\")] int Id){public const string Kept=\"kept\";";
            if(kind=="View"){
                record=record.Replace("Result(","ItemList(",StringComparison.Ordinal)+"[global::System.Diagnostics.DebuggerStepThrough] public static global::System.Collections.Generic.IEnumerable<ItemList> Query(global::UserCode.ManagedDb container)=>/* wrapper comment *//* body before */new ItemList[]{new(1)} /* body after */;}";
            }else{
                record+="}";lambda="/* body before */new global::UserCode.Functions.Result[]{new(id)} /* body after */";member="[global::System.Diagnostics.DebuggerStepThrough] public global::Seamless.Database.TableFunction<global::UserCode.Functions.Result> Find([global::System.ComponentModel.Description(\"argument\")] int id)=>/* wrapper comment */this.TableFunction<global::UserCode.Functions.Result>(()=>"+lambda+");";
            }
            files.Add(new("Result.cs","// record documentation\nnamespace "+ns+"{"+record+"}"));
        }
        if(kind!="View"){if(kind=="ScalarFunction"){member=member.Replace("ScalarFunction<int>","ScalarFunction</* type argument */int>",StringComparison.Ordinal);}var index=files.FindIndex(file=>file.Path=="Database.cs");var text=files[index].Text;files[index]=files[index] with{Text=text.Insert(text.LastIndexOf('}'),"\n// member documentation\n"+member+"\n")};}
        definition=definition with{Source=definition.Source with{Files=files.ToArray()}};
        var query=Designer.ReadQuery(definition,name);var columns=query.Columns.Select(column=>column with{Type="long"}).ToArray();var parameters=query.Parameters.Select(parameter=>parameter with{Type="long"}).ToArray();var returnType=query.ReturnType;if(kind=="ScalarFunction"){returnType="long";}var body=query.Body;if(changeBody){body="/* changed body */"+body;}
        var updated=Designer.ApplyQuery(definition,query with{Columns=columns,Parameters=parameters,ReturnType=returnType,Body=body});var combined=string.Join("\n",updated.Source.Files.Select(file=>file.Text));
        Assert.Contains("DebuggerStepThrough",combined);Assert.Contains("/* wrapper comment */",combined);Assert.Contains("/* body before */",combined);Assert.Contains("/* body after */",combined);Assert.Equal(other,updated.Source.Files.Single(file=>file.Path=="Unrelated.cs").Text);
        if(kind!="ScalarFunction"){Assert.Contains("System.Serializable",combined);Assert.Contains("Description(\"column\")",combined);Assert.Contains("public const string Kept=\"kept\";",combined);Assert.Contains("// record documentation",combined);}
        if(kind!="View"){Assert.Contains("Description(\"argument\")",combined);Assert.Contains("// member documentation",combined);}
        if(kind=="ScalarFunction"){Assert.Collection(System.Text.RegularExpressions.Regex.Matches(combined,@"/\* type argument \*/").Cast<System.Text.RegularExpressions.Match>(),match=>Assert.True(match.Success),match=>Assert.True(match.Success));}
        if(changeBody){Assert.Contains("/* changed body */",combined);}else if(kind=="ScalarFunction"){Assert.Contains("()=>"+lambda,combined);}
        AssertSdkCompilation(updated.Source);
    }
    [Theory]
    [InlineData("",false,false)][InlineData("",true,false)][InlineData("dbo0",false,false)][InlineData("dbo0",true,false)]
    [InlineData("",false,true)][InlineData("",true,true)][InlineData("dbo0",false,true)][InlineData("dbo0",true,true)]
    public void HandwrittenFunctionBodiesWithoutContainerOpenAndKeepTheirBodyDuringStructureSync(string schema,bool block,bool sequence){
        var definition=Designer.Empty("manual-function","Manual function");if(schema.Length>0){definition=Designer.AddSchema(definition,schema);}
        var kind=DatabaseQueryKind.ScalarFunction;var resultType="int";var seed="return default;";ColumnDesign[] columns=[];
        if(sequence){kind=DatabaseQueryKind.TableFunction;resultType="IEnumerable<AnswerResult>";seed="return [];";columns=[new("Id","int",false)];}
        definition=Designer.ApplyQuery(definition,new("Answer",schema,kind,columns,[new("id","int")],resultType,seed));
        var value="id+40";if(sequence){var ns="global::UserCode.Functions";if(schema.Length>0){ns+="."+schema;}value="new "+ns+".AnswerResult[]{new(id)}";}
        var body="/* retained body */ "+value;if(block){body="{/* retained body */var answer="+value+";return answer;}";}
        var receiver="this";if(schema.Length>0){receiver="(global::UserCode.ManagedDb)this.Database";}
        var scaffold="()=>{var container="+receiver+";\n"+seed+"\n}";var source=definition.Source.Files.Single(file=>file.Text.Contains(scaffold,StringComparison.Ordinal));
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>{if(file.Path==source.Path){return file with{Text=file.Text.Replace(scaffold,"()=>"+body,StringComparison.Ordinal)};}return file;}).ToArray()}};
        var path="Answer";if(schema.Length>0){path=schema+"."+path;}
        var query=Designer.ReadQuery(definition,path);Assert.Contains("/* retained body */",query.Body);Assert.DoesNotContain("var container",query.Body);Assert.Single(query.Parameters);
        var target=Designer.Objects(definition).Single(item=>item.Id==path);var design=Designer.ReadObjectDefinition(definition,target);var editedColumns=design.Columns.Select(column=>column with{Type="long"}).ToArray();var editedReturn="long";if(sequence){editedReturn=design.ReturnType;}
        var edit=Designer.ApplyObjectStructure(definition,design,editedColumns,[new("id","long")],editedReturn);Assert.True(edit.Succeeded);Assert.Contains("()=>"+body,edit.Definition!.Source.Files.Single(file=>file.Path==source.Path).Text);
        using(var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(edit.Definition.Source)){Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");}
        var rebuilt=Designer.ApplyQuery(definition,query);Assert.DoesNotContain("var container=",rebuilt.Source.Files.Single(file=>file.Path==source.Path).Text);Assert.Contains("/* retained body */",Designer.ReadQuery(rebuilt,path).Body);
        using var roundtrip=new Seamless.Management.Server.RuntimeCompiler().Compile(rebuilt.Source);Assert.DoesNotContain(roundtrip.Result.Diagnostics,item=>item.Severity=="Error");
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void HandwrittenContainerBindingsKeepTheirMeaningAfterQueryFormRoundtrip(bool local){
        var definition=Designer.Empty("binding","Binding");
        var member="private object? container=null;public global::Seamless.Database.ScalarFunction<int> Answer()=>this.ScalarFunction<int>(()=>container is null?1:2);";
        var expected=1;if(local){member="public global::Seamless.Database.ScalarFunction<int> Answer()=>this.ScalarFunction<int>(()=>{var container=41;return container+1;});";expected=42;}
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>{if(file.Path=="Database.cs"){var index=file.Text.LastIndexOf('}');return file with{Text=file.Text.Insert(index,member)};}return file;}).ToArray()}};
        var query=Designer.ReadQuery(definition,"Answer");var rebuilt=Designer.ApplyQuery(definition,query);
        foreach(var candidate in new[]{definition,rebuilt}){
            using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(candidate.Source);Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");var context=new Seamless.Management.Server.ManagedLoadContext();
            try{using var image=new MemoryStream(compiled.AssemblyBytes);var assembly=context.LoadFromStream(image);var type=assembly.GetType("UserCode.ManagedDb")!;using var database=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(type,[Seamless.Database.DatabaseContainerOptions.InMemory with{IndexSize=1024,PageSize=65536,SegmentSize=1048576,MemorySize=1048576}])!;using var read=database.BeginReadSession();var function=type.GetMethod("Answer")!.Invoke(database,null)!;Assert.Equal(expected,function.GetType().GetProperty("Value")!.GetValue(function));}finally{context.Unload();}
        }
    }
    [Theory][InlineData("")][InlineData("dbo0")]
    public void ExpressionLambdaViewsOpenAndStructureEditsPreserveTheOriginalExpressionAndComments(string schema){
        var definition=Designer.Empty("expression-view","Expression view");if(schema.Length>0){definition=Designer.AddSchema(definition,schema);}
        var ns="UserCode.Views";var path="ItemList";if(schema.Length>0){ns+="."+schema;path=schema+"."+path;}
        const string expression="/* expression before */ new ItemList[]{new(1,\"Metadata view row\",42)} /* expression after */";
        var text="// hand-written view\nnamespace "+ns+" { public readonly partial record struct ItemList(int Id,string Name,int LinuxNote) { public static global::System.Collections.Generic.IEnumerable<ItemList> Query(global::UserCode.ManagedDb container)=>"+expression+"; } }";
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Views/ItemList.cs",text)]}};
        var query=Designer.ReadQuery(definition,path);Assert.Contains("/* expression before */",query.Body);Assert.Contains("/* expression after */",query.Body);Assert.Contains("new ItemList[]{new(1,\"Metadata view row\",42)};",query.Body);Assert.Contains("return",query.Body);Assert.Equal(text,definition.Source.Files.Single(file=>file.Path=="Views/ItemList.cs").Text);
        var target=Designer.Objects(definition).Single(item=>item.Id==path);var design=Designer.ReadObjectDefinition(definition,target);Assert.True(design.StructureAvailable);
        var columns=design.Columns.Select(column=>{if(column.Name=="Id"){return column with{Type="long"};}return column;}).ToArray();
        var edited=Designer.ApplyObjectStructure(definition,design,columns,[],design.ReturnType);Assert.True(edited.Succeeded);var editedText=edited.Definition!.Source.Files.Single(file=>file.Path=="Views/ItemList.cs").Text;Assert.Contains("long Id",editedText);Assert.Contains("=>"+expression,editedText);Assert.Contains("// hand-written view",editedText);
        using(var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(edited.Definition.Source)){Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");}
        var roundtrip=Designer.ApplyQuery(definition,query);Assert.Equal(query.Body,Designer.ReadQuery(roundtrip,path).Body.Trim());
        using var rebuilt=new Seamless.Management.Server.RuntimeCompiler().Compile(roundtrip.Source);Assert.DoesNotContain(rebuilt.Result.Diagnostics,item=>item.Severity=="Error");
    }

    [Fact]public void ThrowExpressionLambdaViewsRemainThrowStatementsWhenReadAndRebuilt(){
        var definition=Designer.Empty("throw-view","Throw view");
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Views/Failure.cs","namespace UserCode.Views{public readonly partial record struct Failure(int Id){public static global::System.Collections.Generic.IEnumerable<Failure> Query(global::UserCode.ManagedDb container)=>throw new global::System.InvalidOperationException(\"view failure\");}}") ]}};
        var query=Designer.ReadQuery(definition,"Failure");Assert.Contains("throw new",query.Body);Assert.DoesNotContain("return throw",query.Body);
        using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(Designer.ApplyQuery(definition,query).Source);Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
    }
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

    [Fact]public void ViewColumnsKeepTheQueryBodyAndTheCustomMembers(){
        var definition=Declared() with{Source=new("1",[new("View.cs","namespace Sample{internal static partial class DatabaseGenerator;} namespace Sample.Views{public partial record struct Summary(int Id){public string Note()=>\"retained\";public static object Query(object container){/* original query */return container;}}}")],[])};
        var target=new CatalogObject("Summary","Summary","","View","global::Sample.Views.Summary");
        var design=Designer.ReadObjectDefinition(definition,target);
        var result=Designer.ApplyObjectStructure(definition,design,[design.Columns[0] with{Type="long"}],[],design.ReturnType);
        Assert.True(result.Succeeded);Assert.Contains("/* original query */return container;",result.Definition!.Source.Files[0].Text);Assert.Contains("Note()=>\"retained\"",result.Definition.Source.Files[0].Text);
    }

    [Fact]public void DatabaseFunctionMetadataEditsInferTheWrapperAndKeepTheMethodBody(){
        var definition=Designer.Empty("db","DB");
        definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"decimal","/* keep body */return id;"));
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var design=Designer.ReadObjectDefinition(definition,target);Assert.Equal("int",design.ReturnType);
        var signatureOnly=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("global::Seamless.Database.ScalarFunction<int>","global::Seamless.Database.ScalarFunction<double>",StringComparison.Ordinal)}).ToArray()};
        using(var broken=new Seamless.Management.Server.RuntimeCompiler().Compile(signatureOnly)){Assert.Contains(broken.Result.Diagnostics,item=>item.Code=="CS0029");}
        var result=Designer.ApplyObjectStructure(definition,design,[],[new("id","long")],"double");
        Assert.True(result.Succeeded);var text=result.Definition!.Source.Files.Single(file=>file.Path=="Database.cs").Text;
        Assert.Contains("ScalarFunction<long>",text);Assert.Equal("long",Assert.Single(Designer.ReadObjectDefinition(result.Definition,target).Parameters).Type);Assert.Contains("/* keep body */return id;",text);
        Assert.Contains("this.ScalarFunction<long>",text);
        using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(result.Definition.Source);Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
    }

    [Fact]public void TableFunctionElementChangeUpdatesItsConstructionAndCompilesWithoutChangingTheBody(){
        var definition=Designer.Empty("db","DB");definition=Designer.ApplyQuery(definition,new("Find","",DatabaseQueryKind.TableFunction,[new("Id","int",false)],[],"IEnumerable<FindResult>","/* preserved table body */return [];"));
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Alternate.cs","namespace UserCode.Functions{public readonly record struct Alternate(int Id);}")]}};
        var target=Designer.Objects(definition).Single(item=>item.Name=="Find");var design=Designer.ReadObjectDefinition(definition,target);
        var result=Designer.ApplyObjectStructure(definition,design,design.Columns,[],"global::System.Collections.Generic.IEnumerable<global::UserCode.Functions.Alternate>");
        Assert.True(result.Succeeded);var text=result.Definition!.Source.Files.Single(file=>file.Path=="Database.cs").Text;
        Assert.Contains("this.TableFunction<global::UserCode.Functions.Alternate>",text);Assert.Contains("/* preserved table body */return [];",text);
        Assert.Contains("Alternate",Designer.ReadObjectDefinition(result.Definition,target).ReturnType);
        using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(result.Definition.Source);Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
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
            Assert.Contains("引数は構造画面で編集し、戻り型はC#の処理本体で指定します。",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
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
