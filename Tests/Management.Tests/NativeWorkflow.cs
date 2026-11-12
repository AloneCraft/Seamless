using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class NativeWorkflow{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("テスト領域が必要です。"),Guid.NewGuid().ToString("N"));
    internal static Definition Database()=>new("db","Native",DefinitionKind.Database,"Model.Db","",new("1",[new("schema.cs","""
        using Seamless.Database;
        namespace Model{
        internal static partial class DatabaseGenerator;
        public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){public Table<Keys.Row,Records.Row> Rows=>this.Table<Keys.Row,Records.Row>(row=>new(row.Id));}
        namespace Keys{public partial struct Row{public partial int Id{get;}}}
        namespace Records{public partial struct Row{public partial int Id{get;}public partial int Value{get;}}}
        }
        """)],[]));
    private static Definition Query(string id,string body,bool writes=false)=>new(id,"Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using System.Linq;using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context){"+body+"}}")],[]),writes);
    internal static OperationInfo Publish(Seamless.Management.Server.ManagementService service,UserAccount user,Definition definition,long revision=0,long version=0){var draft=service.SaveDraft(user,new(definition,revision,version));var preview=service.Preview(user,new(definition.Id,draft.Revision));Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));var result=service.Apply(user,new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()));Assert.True(result.State==OperationState.Succeeded,result.State+": "+result.Error);return result;}
    private static ExecuteRequest Request(string id,string? validation=null,long? revision=null)=>new(id,validation,JsonSerializer.SerializeToElement(new{}),DraftRevision:revision);
    [Fact]public void TransactionsAndRecovery(){
        var root=Root();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Publish(service,user,Database());
            var insert=Query("insert","context.GetDatabase<Db>().Rows.Add(new Records.Row(1,9));return 9;",true);Publish(service,user,insert);
            Assert.Equal(9,Assert.Single(service.Execute(user,Request("insert"),CancellationToken.None)).Value.GetInt32());
            var failure=Query("fail","context.GetDatabase<Db>().Rows.Add(new Records.Row(2,90));throw new System.InvalidOperationException(\"failure\");",true);Publish(service,user,failure);
            Assert.ThrowsAny<Exception>(()=>service.Execute(user,Request("fail"),CancellationToken.None).ToArray());
            var forbidden=Query("readonly","context.GetDatabase<Db>().Rows.Add(new Records.Row(3,900));return 1;");Publish(service,user,forbidden);
            Assert.ThrowsAny<Exception>(()=>service.Execute(user,Request("readonly"),CancellationToken.None).ToArray());
            var sum=Query("sum","return context.GetDatabase<Db>().Rows.Sum(row=>row.Value);");Publish(service,user,sum);Assert.Equal(9,Assert.Single(service.Execute(user,Request("sum"),CancellationToken.None)).Value.GetInt32());
            var draft=service.SaveDraft(user,new(Database() with{Source=Database().Source with{Revision="2"}},1,1));var validation=service.CreateValidationDraft(user,new("db",draft.Revision));Assert.True(validation.FromDraft);Assert.Equal(draft.Definition.Source.Hash(),validation.SourceHash);
            var queryDraft=service.SaveDraft(user,new(sum,1,1));Assert.Equal(0,Assert.Single(service.Execute(user,Request("sum",validation.Id,queryDraft.Revision),CancellationToken.None)).Value.GetInt32());
            Assert.True(service.Preview(user,new("sum",queryDraft.Revision)).Validation?.Passed);var other=service.CreateValidation(user,"db");service.DropValidation(user,validation.Id);Assert.Throws<ManagementException>(()=>service.Seed(user,new(validation.Id,"Rows",[])));Assert.Contains(service.Validations(user).Items,item=>item.Id==other.Id);Assert.Equal(9,Assert.Single(service.Execute(user,Request("sum"),CancellationToken.None)).Value.GetInt32());
            service.ChangeUser(user,new("viewer","ReaderPassword-123",[new("Native",ManagementPermission.View|ManagementPermission.Execute)]));var reader=service.Authenticate(service.Login(new("viewer","ReaderPassword-123")).Token);
            Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.AnalyzePublished(reader,"sum")).Code);Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.Plans(reader,"sum")).Code);Assert.Throws<ManagementException>(()=>service.Seed(reader,new(validation.Id,"Rows",[])));Assert.Throws<ManagementException>(()=>service.Build(reader,new("sum",queryDraft.Revision)));
            var expanded=Database() with{Source=Database().Source with{Revision="3",Files=Database().Source.Files.Select(file=>file with{Text=file.Text.Replace("public partial int Value{get;}","public partial int Value{get;}public partial long Extra{get;}",StringComparison.Ordinal)}).ToArray()}};
            var brokenDependency=service.SaveDraft(user,new(expanded,2,1));Assert.False(service.Preview(user,new("db",brokenDependency.Revision)).CanApply);Assert.Equal(1,service.GetDefinition(user,"db").Version);
            var extraTable=Database() with{Source=Database().Source with{Revision="4",Files=Database().Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<Keys.Row,Records.Row> Rows=>","public Table<Keys.Row,Records.Row> Other=>this.Table<Keys.Row,Records.Row>(row=>new(row.Id));public Table<Keys.Row,Records.Row> Rows=>",StringComparison.Ordinal)}).ToArray()}};
            Publish(service,user,extraTable,3,1);Assert.Equal(2,service.GetDefinition(user,"sum").Version);Assert.Equal(9,Assert.Single(service.Execute(user,Request("sum"),CancellationToken.None)).Value.GetInt32());
            var incompatible=extraTable with{Source=extraTable.Source with{Revision="5",Files=extraTable.Source.Files.Select(file=>file with{Text=file.Text.Replace("public partial int Value{get;}","public partial long Value{get;}",StringComparison.Ordinal)}).ToArray()}};
            var rejected=service.SaveDraft(user,new(incompatible,4,2));var impact=service.Preview(user,new("db",rejected.Revision));Assert.False(impact.CanApply);Assert.Contains(impact.Diagnostics,item=>item.Code=="MigrationRequired");Assert.Equal(2,service.GetDefinition(user,"db").Version);
        }
        var backup=Root();var restored=Root();Seamless.Management.Server.Program.CopyOffline(root,backup);Seamless.Management.Server.Program.CopyOffline(backup,restored);
        using(var store=new ManagementStore(restored))using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Assert.Equal(9,Assert.Single(service.Execute(user,Request("sum"),CancellationToken.None)).Value.GetInt32());Assert.Throws<IOException>(()=>Seamless.Management.Server.Program.CopyOffline(restored,Root()));
        }
    }
    [Fact]public void ConflictsAndCodePolicy(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Publish(service,user,Database());
        Assert.Throws<ManagementException>(()=>service.SaveDraft(user,new(Database() with{Id="another"},0,0)));
        var definition=Query("q","return 1;");var draft=service.SaveDraft(user,new(definition,0,0));var preview=service.Preview(user,new("q",draft.Revision));var request=new ApplyRequest("q",draft.Revision,preview.Token,Guid.NewGuid());Assert.Equal(OperationState.Succeeded,service.Apply(user,request).State);
        Assert.Throws<ManagementException>(()=>service.Apply(user,request with{PreviewToken="other"}));
        service.ChangeUser(user,new("runner","RunnerPassword-123",[new("Native",ManagementPermission.Execute)]));var runner=service.Authenticate(service.Login(new("runner","RunnerPassword-123")).Token);Assert.Throws<ManagementException>(()=>service.GetDefinition(runner,"q"));Assert.Equal(1,Assert.Single(service.Execute(runner,Request("q"),CancellationToken.None)).Value.GetInt32());
        var changed=service.SaveDraft(user,new(definition,1,0));Assert.False(service.Preview(user,new("q",changed.Revision)).CanApply);
        foreach(var code in new[]{"System.IO.File.ReadAllText(\"x\")","System.Environment.GetEnvironmentVariable(\"x\")","System.Type.GetType(\"x\")","System.Activator.CreateInstance<object>()","new System.Xml.XmlDocument()","new System.Diagnostics.TextWriterTraceListener(\"outside.txt\")"}){
            var source=new SourceSnapshot("1",[new("forbidden.cs","public static class Code{public static object? Run()=>"+code+";}")],[]);var compiled=new RuntimeCompiler().Compile(source);Assert.Empty(compiled.AssemblyBytes);Assert.Contains(compiled.Result.Diagnostics,item=>item.Code=="ManagementCodePolicy");
        }
        foreach(var baseCall in new[]{"DatabaseContainer()","DatabaseContainer(options with{StorageDirectory=\"outside\"})"}){var schemaDefinition=Database();var source=schemaDefinition.Source with{Files=schemaDefinition.Source.Files.Select(file=>file with{Text=file.Text.Replace("DatabaseContainer(options)",baseCall,StringComparison.Ordinal)}).ToArray()};var compiled=new RuntimeCompiler().Compile(source);Assert.Empty(compiled.AssemblyBytes);Assert.Contains(compiled.Result.Diagnostics,item=>item.Code=="ManagementCodePolicy");}
    }
    [Fact]public async Task PublicationWaiter(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Publish(service,user,Database());Publish(service,user,Query("sum","return context.GetDatabase<Db>().Rows.Sum(row=>row.Value);"));
        var updated=Database() with{Source=Database().Source with{Revision="2"}};var draft=service.SaveDraft(user,new(updated,1,1));var preview=service.Preview(user,new("db",draft.Revision));
        using var prepared=new ManualResetEventSlim();using var proceed=new ManualResetEventSlim();service.PublicationCheckpoint=(phase,_)=>{if(phase=="Prepared"){prepared.Set();Assert.True(proceed.Wait(TimeSpan.FromSeconds(30)));}};
        var publication=Task.Run(()=>service.Apply(user,new("db",draft.Revision,preview.Token,Guid.NewGuid())));Assert.True(prepared.Wait(TimeSpan.FromSeconds(30)));
        using var admission=new ManualResetEventSlim();service.BeforeQueryAdmission=admission.Set;var execution=Task.Run(()=>service.Execute(user,Request("sum"),CancellationToken.None).ToArray());Assert.True(admission.Wait(TimeSpan.FromSeconds(30)));proceed.Set();Assert.Equal(OperationState.Succeeded,(await publication).State);Assert.Equal(0,Assert.Single(await execution).Value.GetInt32());
    }
    [Fact]public void ResultFieldsAndUnload(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Publish(service,user,Database());
        var definition=new Definition("result","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("result.cs","""
            using Seamless.Management.Server;namespace Model;
            public sealed class Result{public int Number=42;public string Text{get;}="value";}
            public sealed class Input{public int Number;}
            public static class Query{public static object Run(ManagementExecutionContext context,Input input)=>new Result{Number=input.Number};}
            """)],[]));Publish(service,user,definition);ExecuteResult(service,user);
        var weak=service.LastQueryContext!;for(var index=0;index<5&&weak.IsAlive;index++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}Assert.False(weak.IsAlive,"結果のJSONメタデータが生成アセンブリを保持しています。");
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]private static void ExecuteResult(Seamless.Management.Server.ManagementService service,UserAccount user){var result=Assert.Single(service.Execute(user,Request("result") with{Arguments=ManagementJson.Element(new{input=new{number=42}})},CancellationToken.None));Assert.Equal(42,result.Value.GetProperty("number").GetInt32());Assert.Equal("value",result.Value.GetProperty("text").GetString());}
}
