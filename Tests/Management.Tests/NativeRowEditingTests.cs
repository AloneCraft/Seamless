using Seamless.Database;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class NativeRowEditingTests{
    [Fact]public async Task PagesCoverEveryRowOnceAndRejectChangedPrefixes(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"paging-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        var user=Admin(service);var definition=NativeWorkflow.Database();
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<Keys.Row,Records.Row> Rows=>","public Table<Keys.Row,Records.Row> Empty=>this.Table<Keys.Row,Records.Row>(row=>new(row.Id));public Table<Keys.Row,Records.Row> Rows=>")}).ToArray()}};
        NativeWorkflow.Publish(service,user,definition);
        var empty=await service.ReadRowsAsync(user,new("Native","Rows"));
        Assert.Empty(empty.Rows);Assert.False(empty.HasMore);Assert.Null(empty.NextCursor);
        Assert.Contains(empty.KeyMembers,column=>column.Name=="id");Assert.Equal(2,empty.Columns!.Length);
        await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),Enumerable.Range(1,205).Select(id=>Insert("Rows",new{Id=id,Value=id*10})).ToArray()));
        var page=await service.ReadRowsAsync(user,new("Native","Rows"));
        var initial=page;var rows=page.Rows.ToList();Assert.Equal(100,page.Rows.Length);
        while(page.HasMore){
            Assert.NotNull(page.NextCursor);
            var request=new TableRowsRequest("Native","Rows",100,page.Version){Cursor=page.NextCursor};
            request=ManagementJson.Read<TableRowsRequest>(ManagementJson.Element(request));
            page=await service.ReadRowsAsync(user,request);rows.AddRange(page.Rows);
        }
        Assert.Null(page.NextCursor);Assert.Equal(5,page.Rows.Length);
        Assert.Equal(Enumerable.Range(1,205),rows.Select(row=>row.Key.GetProperty("id").GetInt32()).Order());
        var exact=await service.ReadRowsAsync(user,new("Native","Rows",Key:rows[0].Key));
        Assert.Single(exact.Rows);Assert.False(exact.HasMore);Assert.Null(exact.NextCursor);
        var selected=rows[0];await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[
            new("Rows",TableRowChangeKind.Replace,selected.Key,selected.Stamp,ManagementJson.Element(new{Id=selected.Key.GetProperty("id").GetInt32(),Value=-1}))]));
        var error=await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(user,new("Native","Rows",ExpectedVersion:1){Cursor=initial.NextCursor}));
        Assert.Equal("RowConflict",error.Code);
        var refreshed=await service.ReadRowsAsync(user,new("Native","Rows"));Assert.Equal(100,refreshed.Rows.Length);
        Assert.Equal("InvalidRequest",(await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(user,new("Native","Rows"){Cursor=new(1,"bad")}))).Code);
    }
    [Fact]public async Task CyclesCommitAsOneBatchAndOrphansRollBackEveryTable(){
        var root=Root();
        var definition=new Definition("relations","Relations",DefinitionKind.Database,"Relations.Db","",new("1",[new("relations.cs",NativeRelations.Source)],[]));
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Nodes","Graph",[new("Id","int",true),new("Parent","int",false)],
            [new("Self","Graph.Nodes",["Parent"],["Id"])]));
        var committed=Guid.NewGuid();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=Admin(service);
            NativeWorkflow.Publish(service,user,definition);
            var batch=new TableRowBatch("Relations",1,committed,[
                Insert("As",new{Id=1,Other=2}),Insert("Bs",new{Id=2,Other=1}),
                Insert("Graph.Nodes",new{Id=3,Parent=4}),Insert("Graph.Nodes",new{Id=4,Parent=3})
            ]);
            var result=await service.CommitRowsAsync(user,batch);
            Assert.Equal(4,result.ChangedRows);
            Assert.Equal(committed,result.OperationId);
            await Assert.ThrowsAsync<ForeignKeyViolationException>(()=>service.CommitRowsAsync(user,new("Relations",1,Guid.NewGuid(),[
                Insert("Graph.Nodes",new{Id=5,Parent=5}),Insert("As",new{Id=6,Other=999})
            ])));
            Assert.Empty((await service.ReadRowsAsync(user,new("Relations","Graph.Nodes",Key:ManagementJson.Element(new{Id=5})))).Rows);
            var page=await service.ReadRowsAsync(user,new("Relations","Graph.Nodes",Limit:1));
            Assert.Single(page.Rows);
            Assert.True(page.HasMore);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            AuditEvent[] events;
            do{
                events=service.AuditEvents(user,new(Database:"Relations",Action:AuditAction.DataChange)).Items;
                if(events.Length==0){await Task.Delay(50,deadline.Token);}
            }while(events.Length==0);
            Assert.Equal(committed,Assert.Single(events).OperationId);
        }
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=Admin(service);
            var first=Assert.Single((await service.ReadRowsAsync(user,new("Relations","As"))).Rows);
            var second=Assert.Single((await service.ReadRowsAsync(user,new("Relations","Bs"))).Rows);
            await service.CommitRowsAsync(user,new("Relations",1,Guid.NewGuid(),[
                new("As",TableRowChangeKind.Delete,first.Key,first.Stamp),new("Bs",TableRowChangeKind.Delete,second.Key,second.Stamp)
            ]));
            Assert.Empty((await service.ReadRowsAsync(user,new("Relations","As"))).Rows);
            Assert.Empty((await service.ReadRowsAsync(user,new("Relations","Bs"))).Rows);
            Assert.Equal(2,(await service.ReadRowsAsync(user,new("Relations","Graph.Nodes"))).Rows.Length);
        }
    }

    [Fact]public async Task StaleRowsAndGenerationsCannotOverwriteCurrentData(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        var user=Admin(service);
        var definition=NativeWorkflow.Database();
        NativeWorkflow.Publish(service,user,definition);
        await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[
            Insert("Rows",new{Id=1,Value=10}),Insert("Rows",new{Id=2,Value=20})
        ]));
        var first=await Read(1);
        var second=await Read(2);
        await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[
            new("Rows",TableRowChangeKind.Replace,first.Key,first.Stamp,ManagementJson.Element(new{Id=2,Value=11})),
            new("Rows",TableRowChangeKind.Replace,second.Key,second.Stamp,ManagementJson.Element(new{Id=1,Value=22}))
        ]));
        Assert.Equal(22,(await Read(1)).Values.GetProperty("value").GetInt32());
        Assert.Equal("RowConflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.CommitRowsAsync(user,
            new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Delete,first.Key,first.Stamp)])))).Code);

        NativeWorkflow.Publish(service,user,definition with{Source=definition.Source with{Revision="next"}},1,1);
        Assert.Equal("SchemaMismatch",(await Assert.ThrowsAsync<ManagementException>(()=>service.CommitRowsAsync(user,
            new("Native",1,Guid.NewGuid(),[Insert("Rows",new{Id=3,Value=30})])))).Code);
        Assert.Equal(2,(await service.ReadRowsAsync(user,new("Native","Rows"))).Rows.Length);

        async Task<TableRowSnapshot> Read(int id)=>Assert.Single((await service.ReadRowsAsync(user,new("Native","Rows",Key:ManagementJson.Element(new{Id=id})))).Rows);
    }

    [Fact]public async Task PublishedRowAccessRequiresPermissionsAndExcludesSystemData(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        var admin=Admin(service);
        NativeWorkflow.Publish(service,admin,NativeWorkflow.Database());
        var viewer=new UserAccount("viewer","",[new("Native",ManagementPermission.View|ManagementPermission.Execute|ManagementPermission.Edit)]);
        var reader=viewer with{Grants=[new("Native",ManagementPermission.View|ManagementPermission.ReadRows)]};
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(async()=>await service.ReadRowsAsync(viewer,new("Native","Rows")))).Code);
        Assert.Empty((await service.ReadRowsAsync(reader,new("Native","Rows"))).Rows);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(async()=>await service.CommitRowsAsync(reader,
            new("Native",1,Guid.NewGuid(),[Insert("Rows",new{Id=1,Value=1})])))).Code);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(async()=>await service.ReadRowsAsync(admin,new("system","Entries")))).Code);
        var legacyAdministrator=admin with{Grants=[new("*",(ManagementPermission)63)]};
        Assert.Empty((await service.ReadRowsAsync(legacyAdministrator,new("Native","Rows"))).Rows);
    }

    private static TableRowChange Insert(string table,object values)=>new(table,TableRowChangeKind.Insert,Values:ManagementJson.Element(values));
    private static UserAccount Admin(Seamless.Management.Server.ManagementService service)=>service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
}
