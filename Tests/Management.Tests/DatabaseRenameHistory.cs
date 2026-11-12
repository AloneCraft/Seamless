using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Xunit.Abstractions;
using Service=Seamless.Management.Server.ManagementService;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class DatabaseRenameHistory(ITestOutputHelper output){
    [Fact]
    public void HistoryObservesCancellationWhileScanningWithoutMatches(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"history-cancel-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        store.Write(()=>{
            for(var index=0;index<20;index++){
                store.AddHistory("admin","Before","Existing","event-"+index,index);
            }
        });
        using var cancellation=new CancellationTokenSource();
        var scanned=0;
        var error=Assert.Throws<OperationCanceledException>(()=>store.History(0,100,_=>{
            if(++scanned==10){cancellation.Cancel();}
            return false;
        },cancellation.Token));
        Assert.Equal(cancellation.Token,error.CancellationToken);
        Assert.Equal(10,scanned);
        Assert.Equal(20,store.History(0,100,static _=>true).Items.Length);
        Assert.Throws<OperationCanceledException>(()=>store.History(20,100,static _=>true,cancellation.Token));
    }

    [Fact]
    public void RenameRetainsTenThousandLegacyHistoryEntriesAcrossRestart(){
        const int count=10_000;
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"rename-history-"+Guid.NewGuid().ToString("N"));
        var user=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var request=new RenameDatabaseRequest("db:db","Before","After",Guid.NewGuid());
        using(var store=ManagementStore.InitializeReadAuditLegacy(root,"admin","TestPassword-123")){
            var version=new DefinitionVersion(Designer.Empty("db","Before"),1,"admin",DateTimeOffset.UtcNow,"immutable-artifact","generations/immutable");
            store.Write(()=>{
                store.Put("active","db",version);
                store.Put("version","db:1",version);
            });
            for(var first=1;first<=count;first+=100){
                var last=Math.Min(count,first+99);
                store.Write(()=>{
                    for(var index=first;index<=last;index++){
                        store.AddHistory("admin","Before","LegacyHistory","event-"+index,index);
                        var subject=index.ToString(CultureInfo.InvariantCulture);
                        var entry=store.Read<HistoryEntry>("history",subject)!;
                        store.Put("history",subject,entry with{DatabaseDefinitionId=null});
                    }
                });
            }
        }
        ManagementStoreUpgrade.UpgradePreparedWal(root,root+"-backup");
        using(var store=new ManagementStore(root))
        using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Equal(6,store.Format);
            var allocated=GC.GetAllocatedBytesForCurrentThread();
            var elapsed=Stopwatch.StartNew();
            var result=service.RenameDatabase(user,request);
            elapsed.Stop();
            output.WriteLine(JsonSerializer.Serialize(new{
                Entries=count,ElapsedMilliseconds=elapsed.Elapsed.TotalMilliseconds,
                AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated,
                store.ResourceBudget.MemoryLimitBytes,store.ResourceBudget.PeakMemoryBytes
            }));
            Assert.Equal("After",result.Name);
            Assert.Equal(result,service.RenameDatabase(user,request));
            AssertLegacyHistory(store,count);
        }
        using(var store=new ManagementStore(root))
        using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Equal("After",service.GetDefinition(user,"db").Database);
            Assert.Equal("After",service.RenameDatabase(user,request).Name);
            AssertLegacyHistory(store,count);
        }
    }

    private static void AssertLegacyHistory(ManagementStore store,int count){
        var cursor=0L;
        var index=0;
        while(true){
            var page=store.History(cursor,200,static entry=>entry.Action=="LegacyHistory");
            foreach(var entry in page.Items){
                index++;
                Assert.Equal(index,entry.Sequence);
                Assert.Equal("event-"+index,entry.DefinitionId);
                Assert.Equal("Before",entry.Database);
                Assert.Equal("db",entry.DatabaseDefinitionId);
            }
            if(!page.HasMore){break;}
            cursor=page.NextCursor;
        }
        Assert.Equal(count,index);
    }
}
