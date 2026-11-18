using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

// 段階7(3)：製品の入口と同じく UseSdkBuilds を有効にすると、DB 定義の下書きは内蔵のコンパイルではなく dotnet SDK でビルドされ、
// ソースプロジェクトの成果物と同じ検査・保管を通して検証・プレビュー・適用に使われる。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7。
[Collection(SourceBuildBudgetCollection.Name)]
public sealed class DraftSdkBuilds{
    [Fact]
    public async Task DatabaseDraftsAreBuiltWithTheSdkAndRejectInvalidCode(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"sdk-drafts-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store){UseSdkBuilds=true};
        Assert.True(service.UseSdkBuilds);
        var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=Seamless.Management.DatabaseDesigner.Empty("sdkdraft","SdkDraft");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Rows","",[new("Id","int",true),new("Value","int",false)],[]));
        // 旧版はソースだけで決めた場所にある DLL を、ホストが更新されても再利用する。
        var sourceHash=definition.Source.Hash();
        var previousBin=Path.Combine(root,"sdk-drafts",sourceHash[..32],"bin");
        Directory.CreateDirectory(previousBin);
        var previousModule=Path.Combine(previousBin,"Seamless.Managed_"+sourceHash[..24]+".dll");
        File.WriteAllBytes(previousModule,[1,2,3]);
        NativeWorkflow.Publish(service,user,definition);
        Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(previousModule));
        // 公開した成果物は SDK のビルドを検査・保管したもの(保管の場所に同じハッシュの包みがある)。
        var published=service.GetDefinition(user,definition.Id);
        Assert.Equal(64,published.ArtifactHash.Length);
        Assert.True(Directory.Exists(Path.Combine(root,"source-builds",published.ArtifactHash)),"SDK のビルドの成果物が保管されていません。");
        Assert.True(Directory.Exists(Path.Combine(root,"sdk-drafts")));
        // 生成コードの表示は、同じソースの SDK のビルドが書き出した生成ファイルを読む(ビルドを使い回す)。
        var generated=service.SdkBuilder!.GenerateSources(definition.Source);
        Assert.Contains(generated,file=>file.Kind=="User");
        Assert.Contains(generated,file=>file.Kind=="Database"&&file.Text.Contains("Rows",StringComparison.Ordinal));
        await service.CommitRowsAsync(user,new(definition.Database,published.Version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=7}))]));
        Assert.Equal(7,Assert.Single((await service.ReadRowsAsync(user,new(definition.Database,"Rows"))).Rows).Values.GetProperty("value").GetInt32());

        // 構文の誤りは、ファイルと位置の付いた診断になり、適用できない。
        var broken=definition with{Source=definition.Source with{Revision="broken",Files=[..definition.Source.Files,new("Broken.cs","namespace UserCode;public static class Broken{public static int Value=>\"text\";}")]}};
        var draft=service.SaveDraft(user,new(broken,1,published.Version));
        var preview=service.Preview(user,new(broken.Id,draft.Revision));
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics,item=>item.Code=="CS0029"&&item.Path=="Broken.cs"&&item.Severity=="Error");

        // 使用制限は SDK のビルドの中で DB 生成器の解析器が誤りにする(プロジェクトの印 SeamlessManagedDatabase)。
        var forbidden=definition with{Source=definition.Source with{Revision="forbidden",Files=[..definition.Source.Files,new("Forbidden.cs","namespace UserCode;public static class Forbidden{public static string Read()=>System.IO.File.ReadAllText(\"x\");}")]}};
        draft=service.SaveDraft(user,new(forbidden,draft.Revision,published.Version));
        preview=service.Preview(user,new(forbidden.Id,draft.Revision));
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics,item=>item.Code==Seamless.Database.Generator.ManagedDatabaseCodePolicyAnalyzer.DiagnosticId&&item.Path=="Forbidden.cs");
    }

    // 段階7(3)：登録クエリも、公開済みの DB の成果物を参照して SDK でビルドする。入口 QueryEntry は DB 生成器がプロジェクトの設定から作り、
    // 監査の情報(入口の識別)はビルドしたソースの解析から作る。DB を公開し直すと登録クエリも SDK で作り直す。下書きの実行も SDK のビルドを使う。
    [Fact]
    public void RegisteredQueriesAreBuiltWithTheSdkAgainstThePublishedDatabase(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"sdk-queries-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store){UseSdkBuilds=true};
        var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
        Definition Query(string id,string body,bool writes=false)=>new(id,"Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using System.Linq;using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context){"+body+"}}")],[]),writes);
        ExecuteRequest Request(string id,string? validation=null,long? revision=null)=>new(id,validation,System.Text.Json.JsonSerializer.SerializeToElement(new{}),DraftRevision:revision);
        NativeWorkflow.Publish(service,user,Query("insert","context.GetDatabase<Db>().Rows.Add(new Tables.Row(new(1),9));return 9;",true));
        Assert.Equal(9,Assert.Single(service.Execute(user,Request("insert"),CancellationToken.None)).Value.GetInt32());
        var sum=Query("sum","return context.GetDatabase<Db>().Rows.Sum(row=>row.Value);");
        NativeWorkflow.Publish(service,user,sum);
        var published=service.GetDefinition(user,"sum");
        Assert.True(Directory.Exists(Path.Combine(root,"source-builds",published.ArtifactHash)),"登録クエリの SDK のビルドの成果物が保管されていません。");
        // 共有依存は版とハッシュを記録し、利用者の成果物へ重複保存しない。
        var bundled=Directory.EnumerateFiles(Path.Combine(root,"source-builds",published.ArtifactHash),"*",SearchOption.AllDirectories).ToArray();
        Assert.DoesNotContain(bundled,file=>Path.GetFileName(file) is "Server.dll"||Path.GetFileName(file).StartsWith("Microsoft.CodeAnalysis",StringComparison.Ordinal));
        Assert.True(bundled.Sum(file=>new FileInfo(file).Length)<20L<<20,"登録クエリの成果物が大きすぎます。");
        using var packageManifest=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"source-builds",published.ArtifactHash,"manifest.json")));
        Assert.Equal(2,packageManifest.RootElement.GetProperty("format").GetInt32());
        var sharedFiles=packageManifest.RootElement.GetProperty("files").EnumerateArray().Where(file=>file.GetProperty("shared").GetBoolean()).ToArray();
        Assert.Contains(sharedFiles,file=>file.GetProperty("name").GetString()=="Database");
        Assert.All(sharedFiles,file=>Assert.False(File.Exists(Path.Combine(root,"source-builds",published.ArtifactHash,file.GetProperty("file").GetString()!))));
        Assert.Equal(9,Assert.Single(service.Execute(user,Request("sum"),CancellationToken.None)).Value.GetInt32());
        var method=service.DefinitionInfo(user,"sum").Method;
        Assert.NotNull(method);
        Assert.Equal("Run",method.MethodName);

        // DB を公開し直すと、登録クエリも新しい DB の成果物を参照して作り直す。
        var extraTable=NativeWorkflow.Database() with{Source=NativeWorkflow.Database().Source with{Revision="2",Files=NativeWorkflow.Database().Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<Keys.Row,Tables.Row> Rows=>","public Table<Keys.Row,Tables.Row> Other=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Id));public Table<Keys.Row,Tables.Row> Rows=>",StringComparison.Ordinal)}).ToArray()}};
        NativeWorkflow.Publish(service,user,extraTable,1,1);
        Assert.Equal(2,service.GetDefinition(user,"sum").Version);
        Assert.Equal(9,Assert.Single(service.Execute(user,Request("sum"),CancellationToken.None)).Value.GetInt32());

        // 下書きの登録クエリを検証DBで実行する。
        var current=service.GetDraft(user,"sum");
        var draft=service.SaveDraft(user,new(sum with{Source=sum.Source with{Revision="2",Files=[new("query.cs",sum.Source.Files[0].Text.Replace("row.Value","row.Value+1",StringComparison.Ordinal))]}},current.Revision,current.BaseVersion));
        var validation=service.CreateValidation(user,"db");
        try{Assert.Equal(0,Assert.Single(service.Execute(user,Request("sum",validation.Id,draft.Revision),CancellationToken.None)).Value.GetInt32());}
        finally{service.DropValidation(user,validation.Id);}

        // 入口が無ければ、DB 生成器の診断で止まる。
        var missing=Query("missing","return 0;") with{EntryMember="Missing"};
        var missingDraft=service.SaveDraft(user,new(missing,0,0));
        var preview=service.Preview(user,new("missing",missingDraft.Revision));
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics,item=>item.Code==Seamless.Database.Generator.ManagedQueryEntryGenerator.DiagnosticId);
    }
}
