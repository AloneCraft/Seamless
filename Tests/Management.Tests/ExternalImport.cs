using Microsoft.Data.SqlClient;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

/// <summary>
/// Manager の外部DBの取り込み(SQL Server)。本物の SQL Server(SEAMLESS_SQLSERVER_TEST_CONNECTION、Windows 認証)の Northwind を、
/// 下見 → 取り込み → 行の読み取り → 置き換え(古い世代はバックアップ)まで通す。生成した C# はプロジェクトとして保存先に書かれる。
/// </summary>
public sealed class ExternalImport{
    private sealed class MemoryCredentials:IExternalCredentialStore{
        public Dictionary<string,string> Saved{get;}=new(StringComparer.Ordinal);
        public string? Read(string target)=>this.Saved.GetValueOrDefault(target);
        public void Write(string target,string userName,string password)=>this.Saved[target]=password;
    }

    private static string? Connection=>Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION");

    [Fact,Trait("Category","LiveSqlServer")]public async Task ImportsNorthwindAndReplacesItWithBackup(){
        if(Connection is null){return;}
        var source=Environment.GetEnvironmentVariable("SEAMLESS_EXTERNAL_IMPORT_DATABASE")??"Northwind";
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"external-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        using var projects=new SourceProjectService();
        var user=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var transfer=new ExternalTransferService(service,projects,new MemoryCredentials());
        // 接続文字列にパスワードを入れると断る(履歴に残さないため)。
        await Assert.ThrowsAsync<ManagementException>(()=>transfer.ConnectAsync(new("SqlServer","Server=localhost;User ID=sa;Password=x"),CancellationToken.None));
        var connection=new ExternalConnectionRequest("SqlServer",Connection);
        var info=await transfer.ConnectAsync(connection,CancellationToken.None);
        Assert.Contains(source,info.Databases,StringComparer.OrdinalIgnoreCase);
        var preview=await transfer.PreviewAsync(user,new(connection,source,"Imported",[]),CancellationToken.None);
        Assert.False(preview.TargetExists);
        Assert.NotEmpty(preview.Tables);
        // 取り込み。生成した C# のプロジェクトを空のフォルダーに書く。
        var first=await RunAsync(transfer,new(new(connection,source,"Imported",[]),preview.Token,Path.Combine(root,"project1"),false,Guid.NewGuid()));
        Assert.True(first.State=="Succeeded",first.Error);
        var report=first.Report!;
        Assert.True(File.Exists(report.ProjectPath),report.ProjectPath);
        Assert.Null(report.Backup);
        // 表ごとの件数が SQL Server と同じ。
        await using(var sql=new SqlConnection(new SqlConnectionStringBuilder(Connection){InitialCatalog=source}.ConnectionString)){
            await sql.OpenAsync();
            foreach(var table in report.Tables){
                await using var command=new SqlCommand($"SELECT COUNT_BIG(*) FROM [{table.Schema}].[{table.Name}]",sql);
                Assert.Equal((long)(await command.ExecuteScalarAsync())!,table.Rows);
            }
        }
        // 取り込んだ DB の表の行を Manager の行の読み取りで読める。
        var largest=report.Tables.OrderByDescending(static table=>table.Rows).First();
        var databaseId=service.Databases(user).Items.Single(static item=>item.Name=="Imported").Id;
        var objects=service.Objects(user,new CatalogRequest(databaseId,NameFilter:largest.Name));
        var tableId=objects.Items.First(item=>item.Kind=="Table"&&item.Name==largest.Name).Id;
        var rows=await service.ReadRowsAsync(user,new TableRowsRequest("Imported",tableId,Limit:10));
        Assert.Equal(Math.Min(10,largest.Rows),rows.Rows.Length);
        // 同じ名前で取り込むと、置き換えを選ばない限り止まる。選べば丸ごと作り直し、古い世代をバックアップに残す。
        var again=await transfer.PreviewAsync(user,new(connection,source,"Imported",[]),CancellationToken.None);
        Assert.True(again.TargetExists);
        var refused=await RunAsync(transfer,new(new(connection,source,"Imported",[]),again.Token,Path.Combine(root,"project2"),false,Guid.NewGuid()));
        Assert.Equal("Failed",refused.State);
        var replaced=await RunAsync(transfer,new(new(connection,source,"Imported",[]),again.Token,Path.Combine(root,"project3"),true,Guid.NewGuid()));
        Assert.True(replaced.State=="Succeeded",replaced.Error);
        Assert.Equal(report.DefinitionId,replaced.Report!.DefinitionId);
        Assert.Equal(report.Version+1,replaced.Report.Version);
        Assert.True(Directory.Exists(replaced.Report.Backup),replaced.Report.Backup);
    }

    private static async Task<ExternalTransferStatus> RunAsync(ExternalTransferService transfer,ExternalImportRequest request){
        var status=transfer.Start(new UserAccount("admin","",[new("*",ManagementPermission.All)]),request);
        var deadline=DateTime.UtcNow.AddMinutes(20);
        while(status.State=="Running"){
            if(DateTime.UtcNow>deadline){throw new TimeoutException("取り込みが終わりません: "+status.Phase);}
            await Task.Delay(200);status=transfer.Status(request.OperationId);
        }
        return status;
    }
}
