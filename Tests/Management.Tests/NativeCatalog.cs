using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class NativeCatalog{
    [Fact]public void AuditProgressDoesNotInvalidateTheObjectCatalogButPermissionChangesDo(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store){AuditDeliveryPaused=true};var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);NativeWorkflow.Publish(service,user,NativeWorkflow.Database());var page=service.Databases(user);var db=page.Items.Single(item=>!item.IsSystem);var request=new CatalogRequest(db.Id,page.VersionToken);var origin=Assert.Single(store.List<AuditOrigin>("auditOrigin").Items);store.Write(()=>store.Put("auditOrigin",origin.WalId.ToString("N"),origin with{DurableLsn=0,AcknowledgedLsn=0}));Assert.NotEmpty(service.Objects(user,request).Items);Assert.Equal(page.VersionToken,service.Databases(user).VersionToken);
        var revision=store.CatalogRevision;Assert.Throws<InvalidOperationException>(()=>store.Write(()=>{store.Put("active","rollback-probe",service.GetDefinition(user,"db"));throw new InvalidOperationException("rollback");}));Assert.Equal(revision,store.CatalogRevision);Assert.NotEmpty(service.Objects(user,request).Items);
        service.ChangeUser(user,new("viewer","ViewerPassword-123",[new("Native",ManagementPermission.View)]));Assert.Equal("CatalogChanged",Assert.Throws<ManagementException>(()=>service.Objects(user,request)).Code);
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
    [Fact]public void ObjectsAndSource(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=NativeWorkflow.Database();var text=definition.Source.Files[0].Text.Replace("public Table<Keys.Row,Tables.Row> Rows=>", "public SalesSchema Sales=>throw new System.InvalidOperationException();public StoredProcedure<int> Rebuild()=>throw new System.InvalidOperationException();public Table<Keys.Row,Tables.Row> Rows=>");// ビュー・関数は生成される入口(Db.IReader)への拡張メンバー。
        text+="\nnamespace Model.Views{public readonly record struct All(int Id,int Value);}namespace Model{public static class DbQueries{extension(Db.IReader q){public global::Seamless.Collections.ISet<Views.All> All=>throw new System.InvalidOperationException();public int Total(int minimum)=>throw new System.InvalidOperationException();public global::Seamless.Collections.ISet<Tables.Row> Find(int minimum)=>throw new System.InvalidOperationException();}}}";
        text=text.Replace("public SalesSchema Sales=>","public EmptySchema Empty=>throw new System.InvalidOperationException();public SalesSchema Sales=>").Replace("public Table<Keys.Row,Tables.Row> Rows=>","[ImportedDatabaseObject(\"Imported.Schema\",\"OriginalRows\")]public Table<Keys.Row,Tables.Row> Rows=>");
        text+="\nnamespace Model{public sealed class EmptySchema(DatabaseContainer db):DatabaseSchema(db,\"Empty\");public sealed class SalesSchema(DatabaseContainer db):DatabaseSchema(db,\"Sales\"){public Table<Keys.Row,Tables.Row> Orders=>throw new System.InvalidOperationException();}}";
        NativeWorkflow.Publish(service,user,definition with{Source=definition.Source with{Files=[new("schema.cs",text)]}});
        var databases=service.Databases(user);Assert.Equal(2,databases.Items.Length);var db=databases.Items.Single(item=>!item.IsSystem);var request=new CatalogRequest(db.Id,databases.VersionToken);
        Assert.Equal(new[]{"","Empty","Imported.Schema","Sales"},service.Schemas(user,request).Items.Select(item=>item.Name));
        var objects=service.Objects(user,request).Items;Assert.Contains(objects,item=>item.Name=="Orders"&&item.Schema=="Sales");Assert.Contains(objects,item=>item.Name=="OriginalRows"&&item.Schema=="Imported.Schema"&&item.Id=="Rows");
        Assert.All(new[]{"Table","View","ScalarFunction","TableFunction","StoredProcedure","SystemTable"},kind=>Assert.Contains(objects,item=>item.Kind==kind));
        var details=service.ObjectDetails(user,request with{ObjectId="Sales.Orders"});Assert.Contains(details.Columns,item=>item.Name=="Value"&&item.Type=="System.Int32");Assert.Equal("Id",Assert.Single(details.KeyMembers).Name);
        var source=service.ObjectSource(user,request with{ObjectId="Sales.Orders"});Assert.True(source.Location.Available);Assert.Equal(db.ArtifactHash,source.Location.ArtifactHash);Assert.Equal("Orders",source.Definition!.Definition.Source.Files[0].Text.Substring(source.Location.Start,source.Location.Length));
        Assert.Equal("minimum",Assert.Single(service.ObjectDetails(user,request with{ObjectId="Total"}).Parameters).Name);
        var system=service.Objects(user,new("system",databases.VersionToken));Assert.Contains(system.Items,item=>item.Name=="Entries"&&item.Kind=="SystemTable");Assert.False(service.ObjectSource(user,new("system",databases.VersionToken,ObjectId:"Entries")).Location.Available);
        service.ChangeUser(user,new("viewer","ViewerPassword-123",[new("Native",ManagementPermission.View)]));var viewer=service.Authenticate(service.Login(new("viewer","ViewerPassword-123")).Token);
        Assert.Single(service.Databases(viewer).Items);Assert.Throws<ManagementException>(()=>service.Objects(viewer,new("system")));Assert.Throws<ManagementException>(()=>service.Objects(viewer,new("db:unknown")));
        Assert.Equal("CatalogChanged",Assert.Throws<ManagementException>(()=>service.Objects(user,request)).Code);
    }
    [Fact]public void PagesAndPublishedIdentity(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=NativeWorkflow.Database();var source=definition.Source.Files[0].Text+"\nnamespace Model.Views{"+string.Concat(Enumerable.Range(0,205).Select(index=>"public readonly record struct V"+index.ToString("D3")+"(int Id);"))+"}"
            +"\nnamespace Model{public static class DbQueries{extension(Db.IReader q){"+string.Concat(Enumerable.Range(0,205).Select(index=>"public global::Seamless.Collections.ISet<Views.V"+index.ToString("D3")+"> V"+index.ToString("D3")+"=>throw new System.InvalidOperationException();"))+"}}}";
        NativeWorkflow.Publish(service,user,definition with{Source=definition.Source with{Files=[new("schema.cs",source)]}});
        var db=service.Databases(user).Items.Single(item=>!item.IsSystem);var request=new CatalogRequest(db.Id,Kind:"View");var first=service.Objects(user,request);Assert.Equal(100,first.Items.Length);Assert.True(first.HasMore);
        var second=service.Objects(user,request with{VersionToken=first.VersionToken,Cursor=first.NextCursor});var third=service.Objects(user,request with{VersionToken=first.VersionToken,Cursor=second.NextCursor});Assert.Equal(100,second.Items.Length);Assert.Equal(5,third.Items.Length);Assert.False(third.HasMore);Assert.Equal(205,first.Items.Concat(second.Items).Concat(third.Items).Select(item=>item.Id).Distinct().Count());
        service.SaveDraft(user,new(definition with{Source=definition.Source with{Revision="2"}},1,1));var published=service.ObjectSource(user,new(db.Id,ObjectId:"Rows"));Assert.Equal(source,published.Definition!.Definition.Source.Files[0].Text);
        Assert.Equal(first.VersionToken,service.Objects(user,request with{VersionToken=first.VersionToken}).VersionToken);
        NativeWorkflow.Publish(service,user,definition with{Source=definition.Source with{Revision="2"}},2,1);Assert.Equal("CatalogChanged",Assert.Throws<ManagementException>(()=>service.Objects(user,request with{VersionToken=first.VersionToken})).Code);
    }
}
