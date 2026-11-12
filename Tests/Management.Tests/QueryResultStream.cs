using System.Text.Json;
using Seamless.Execution;
using Seamless.Management;

namespace Management.Tests;

public sealed class QueryResultStream{
    [Fact]public void MemoryAndSpilledResultsAreIndependentSingleUseCopies(){
        foreach(var limit in new[]{65536L,4096L}){
            var parent=new ResourceBudget(1L<<20,1L<<20);var released=0;var root=Root();
            using var result=new Seamless.Management.Server.QueryResultStream<string>(new(){ParentBudget=parent,MemoryLimitBytes=limit,TemporaryStorageLimitBytes=1L<<20,TemporaryDirectory=root},_=>released++);
            var expected=Enumerable.Range(0,40).Select(index=>index+"\n日本語\""+new string('x',100)).ToArray();
            foreach(var value in expected){result.Add(ManagementJson.Element(value));}result.Seal();
            Assert.Equal(40,result.Count);Assert.Equal(limit==4096,result.Spilled);
            Assert.True(parent.MemoryBytes+parent.TemporaryStorageBytes>0);
            using var iterator=result.GetEnumerator();Assert.Throws<InvalidOperationException>(()=>result.GetEnumerator());
            var actual=new List<string>();while(iterator.MoveNext()){actual.Add(iterator.Current);}
            Assert.Equal(expected,actual);Assert.Equal(expected[0],actual[0]);Assert.False(iterator.MoveNext());
            Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,parent.TemporaryStorageBytes);Assert.Equal(1,released);
            Assert.Throws<ObjectDisposedException>(()=>result.GetEnumerator());
            Assert.False(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any());
        }
    }
    [Fact]public void EarlyDisposalCancellationAndLimitsReleaseAllReservations(){
        var parent=new ResourceBudget(1L<<20,1L<<20);var root=Root();
        Seamless.Management.Server.QueryResultStream<string?> Create(CancellationToken token=default,long disk=1L<<20)=>new(new(){ParentBudget=parent,MemoryLimitBytes=4096,TemporaryStorageLimitBytes=disk,TemporaryDirectory=root,CancellationToken=token},_=>{});
        using(var result=Create()){
            result.Add(ManagementJson.Element((string?)null));result.Seal();using var iterator=result.GetEnumerator();
            Assert.True(iterator.MoveNext());Assert.Null(iterator.Current);iterator.Dispose();iterator.Dispose();
        }
        using(var result=Create()){
            result.Add(ManagementJson.Element("unused"));result.Seal();result.GetEnumerator().Dispose();
            Assert.Equal(0,parent.MemoryBytes);
        }
        using(var cancellation=new CancellationTokenSource())using(var result=Create(cancellation.Token)){
            for(var index=0;index<40;index++){result.Add(ManagementJson.Element(new string('a',100)));}result.Seal();
            using var iterator=result.GetEnumerator();Assert.True(iterator.MoveNext());cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(()=>iterator.MoveNext());Assert.Equal(0,parent.TemporaryStorageBytes);
        }
        using(var cancellation=new CancellationTokenSource())using(var result=Create(cancellation.Token)){
            for(var index=0;index<40;index++){result.Add(ManagementJson.Element(new string('a',100)));}result.Seal();cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(()=>result.GetEnumerator());Assert.Equal(0,parent.TemporaryStorageBytes);Assert.Equal(0,parent.MemoryBytes);
        }
        using(var result=Create(disk:0)){
            var error=Assert.Throws<ResourceLimitException>(()=>{for(var index=0;index<40;index++){result.Add(ManagementJson.Element(new string('a',100)));}});
            Assert.Equal(ResourceLimitKind.TemporaryStorage,error.Kind);
        }
        using(var result=Create()){
            Assert.Throws<ResourceLimitException>(()=>result.Add(ManagementJson.Element(new string('a',1000))));
        }
        using(var result=new Seamless.Management.Server.QueryResultStream<int>(new(){ParentBudget=parent,TemporaryDirectory=root},_=>{})){
            Assert.Throws<JsonException>(()=>result.Add(ManagementJson.Element("wrong")));
        }
        Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,parent.TemporaryStorageBytes);
        Assert.False(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any());
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"stream-"+Guid.NewGuid().ToString("N"));
}
