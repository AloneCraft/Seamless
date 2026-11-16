using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

// 段階7(3)：製品の入口と同じく UseSdkBuilds を有効にすると、DB 定義の下書きは内蔵のコンパイルではなく dotnet SDK でビルドされ、
// ソースプロジェクトの成果物と同じ検査・保管を通して検証・プレビュー・適用に使われる。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7。
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
        NativeWorkflow.Publish(service,user,definition);
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
}
