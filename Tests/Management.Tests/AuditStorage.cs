using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class AuditStorage{
    private static string Root(){var root=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("A test root is required.");return Path.Combine(root,"audit-"+Guid.NewGuid().ToString("N"));}
    private static AuditEvent Event(string message="注文の失敗",string subject="user",DateTimeOffset? at=null){var time=at??DateTimeOffset.UtcNow;return new(Guid.NewGuid(),Guid.NewGuid(),time,time,Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),subject,"sales","orders",AuditAction.DataChange,AuditOutcome.Failed,7,"code-v1",null,"Constraint",message,"Company.Sales","OrderQueries","Save","Save(Order)","Orders.Where(x=>x.Id==@p0)");}
    [Fact]public void NativeAuditPersistsAndRejectsDuplicateEventContent(){
        var root=Root();var item=Event();using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            Assert.Equal(4,store.Format);Assert.True(store.HasAudit);var sequence=store.AppendAudit(item);Assert.Equal(sequence,store.AppendAudit(item));Assert.Single(store.AuditEvents(new()).Items);
            Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>store.AppendAudit(item with{Message="different"})).Code);
        }
        using var reopened=new ManagementStore(root);var saved=Assert.Single(reopened.AuditEvents(new()).Items);Assert.Equal(item,saved);
    }
    [Fact]public void SearchFiltersTimeSubjectAndQualifiedMethodMetadata(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");var time=new DateTimeOffset(2026,11,3,12,0,0,TimeSpan.Zero);var wanted=Event(at:time);store.AppendAudit(wanted);store.AppendAudit(Event("other",subject:"other",at:time.AddDays(1)));
        Assert.Equal(wanted.EventId,Assert.Single(store.AuditEvents(new(FromUtc:time.AddHours(-1),ToUtc:time.AddHours(1),SubjectId:"user",Text:"Company.Sales")).Items).EventId);
        Assert.Single(store.AuditEvents(new(Text:"Save(Order)")).Items,item=>item.EventId==wanted.EventId);Assert.Empty(store.AuditEvents(new(SubjectId:"missing")).Items);Assert.Single(store.AuditEvents(new(SubjectId:"user",Text:"注文 失敗")).Items);
        store.AppendAudit(Event("注文成功",at:time));store.AppendAudit(Event("顧客取得失敗",at:time));Assert.Equal(wanted.EventId,Assert.Single(store.AuditEvents(new(Text:"注文 失敗")).Items).EventId);
        Assert.Equal(wanted.EventId,Assert.Single(store.AuditEvents(new(Text:"company.sales.orderqueries.save 注文 失敗")).Items).EventId);Assert.Empty(store.AuditEvents(new(Text:"注文 存在しない語")).Items);
    }
    [Fact]public void PagedAuditDoesNotIncludeLaterAppends(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");for(var i=0;i<5;i++){store.AppendAudit(Event(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));}
        var first=store.AuditEvents(new(Limit:2));Assert.Equal(2,first.Items.Length);store.AppendAudit(Event("later"));var next=store.AuditEvents(new(Limit:10,Before:first.NextBefore,Ceiling:first.Ceiling));Assert.Equal(3,next.Items.Length);Assert.DoesNotContain(next.Items,item=>item.Message=="later");
    }
    [Fact]public void SummaryBatchesAreIdempotentAndKeepVersionsSeparate(){
        var root=Root();var at=new DateTimeOffset(2026,11,3,12,0,0,TimeSpan.Zero);var key=new AuditReadKey(at,at.AddMinutes(1),Guid.NewGuid(),"user","sales","query-v1","code-v1",7);var batch=Guid.NewGuid();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            Assert.True(store.ApplyReadSummaryBatch(batch,[new(key,10)]));Assert.False(store.ApplyReadSummaryBatch(batch,[new(key,10)]));Assert.Equal(10,store.ReadSummary(key)!.Count);
            Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>store.ApplyReadSummaryBatch(batch,[new(key,20)])).Code);Assert.True(store.ApplyReadSummaryBatch(Guid.NewGuid(),[new(key with{PolicyVersion=8},3)]));Assert.Equal(2,store.ReadSummaries(new()).Items.Length);
        }
        using var reopened=new ManagementStore(root);Assert.False(reopened.ApplyReadSummaryBatch(batch,[new(key,10)]));Assert.Equal(10,reopened.ReadSummary(key)!.Count);
    }
    [Fact]public void AuditAndManagementChangesRollBackTogether(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");Assert.Throws<InvalidOperationException>(()=>store.Write(()=>{store.Put("probe","x",new{Value=1});store.AppendAuditInside(Event());throw new InvalidOperationException("rollback");}));Assert.Null(store.Read<object>("probe","x"));Assert.Empty(store.AuditEvents(new()).Items);
    }
    [Fact]public void LegacyStorageIsNotAutomaticallyUpgraded(){
        var root=Root();using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123")){Assert.Equal(3,store.Format);}
        using var reopened=new ManagementStore(root);Assert.False(reopened.HasAudit);Assert.Equal("MigrationRequired",Assert.Throws<ManagementException>(()=>reopened.AppendAudit(Event())).Code);Assert.Equal("3",File.ReadAllText(Path.Combine(root,"management.format")).Trim());
    }
    [Fact]public void SourceRestrictedAuditCannotBeSearchedThroughItsHiddenTemplate(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");store.AppendAudit(Event() with{QueryTemplate="Orders.Where(x=>x.HiddenSalaryPolicy==@p0)"});using var service=new Seamless.Management.Server.ManagementService(store);
        var viewer=new UserAccount("manager","",[new("*",ManagementPermission.Manage)]);var visible=Assert.Single(service.AuditEvents(viewer,new()).Items);Assert.Null(visible.QueryTemplate);Assert.Empty(service.AuditEvents(viewer,new(Text:"HiddenSalaryPolicy")).Items);Assert.Single(service.AuditEvents(viewer,new(Text:"Company.Sales.OrderQueries.Save")).Items);
        var developer=viewer with{Grants=[new("*",ManagementPermission.Manage),new("sales",ManagementPermission.Edit)]};Assert.Single(service.AuditEvents(developer,new(Text:"HiddenSalaryPolicy")).Items);
    }
}
[CollectionDefinition("AuditStorage",DisableParallelization=true)]public sealed class AuditStorageCollection;
