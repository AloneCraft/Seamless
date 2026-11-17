using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

// 表の多い DB も Manager で公開し、どの表にも行を書けること。
// 不具合(2026-11-16 に別のセッションが見つけた)：Manager の設定(表ごとのメモリ 16MB)では、表17個から書けず、20個では開けなかった。
// 共通ストアの上限が DB の上限とは別に既定の 256MB のままで、表ごとのメモリの先取りがそこに収まらなかった。
public sealed class ManyTableDatabases{
    [Theory]
    [InlineData(17)]
    [InlineData(24)]
    public async Task ManagerCanPublishAndWriteADatabaseWithManyTables(int count){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"many-tables-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=Seamless.Management.DatabaseDesigner.Empty("many","Many");
        for(var index=1;index<=count;index++){
            definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("T"+index,"",[new("Id","int",true),new("Value","int",false)],[]));
        }
        NativeWorkflow.Publish(service,user,definition);
        var version=service.GetDefinition(user,definition.Id).Version;
        foreach(var table in new[]{"T1","T"+count}){
            await service.CommitRowsAsync(user,new(definition.Database,version,Guid.NewGuid(),[new(table,TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=7}))]));
            Assert.Equal(7,Assert.Single((await service.ReadRowsAsync(user,new(definition.Database,table))).Rows).Values.GetProperty("value").GetInt32());
        }
    }
}
