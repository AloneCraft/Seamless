using MemoryPack;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeOrdinaryTableQueriesUseFLinqWithoutExplicitTransactionRows(bool versioned){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;
        // 並行する別試験のビルド途中のmanifestを列挙しない。この試験の成果物だけを照合する。
        var buildId="native-auto-build-"+Guid.NewGuid().ToString("N");var buildData=Path.Combine(data,buildId);var compiler=GeneratedAssemblyBuilderTests.Compiler(buildData,Path.Combine(cache,buildId),nativeFLinq:true);
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536};if(versioned){options=Options();}
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"native-auto-rows-"+Guid.NewGuid().ToString("N")),compiler,options,[Definition("b")]);
        var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(49,"b");Execute(engine,session,"INSERT dbo.Items VALUES(1,100); INSERT dbo.Items VALUES(2,200)");var type=((InProcessDatabaseAdapter)host.Databases.Resolve("b").Adapter).Database.GetType().FullName!;
        var source="using System.Linq;using MemoryPack;using Seamless.Database.Client;[Seamless.FLinq.LinqIntercept(true)]public sealed class Query:INativeQueryModule{private static System.Collections.Generic.IComparer<int> Comparison()=>System.Collections.Generic.Comparer<int>.Default;private static int Total(global::"+type+" db){var rows=db.dbo.Items.Where(static row=>row.Id>0).ToArray();var count=db.dbo.Items.Count();if(rows.Length!=count){throw new System.Exception(\"Count mismatch\");}var total=db.dbo.Items.Where(static row=>row.Id>0).Sum(static row=>row.N);var groups=db.dbo.Items.GroupBy(static row=>row.Id%2).ToArray();var arrays=db.dbo.Items.GroupBy(static row=>row.Id%2).Select(static group=>group.ToArray()).ToArray();var ordered=db.dbo.Items.GroupBy(static row=>row.Id%2).OrderByDescending(static group=>group.Key).ThenBy(static group=>group.Count()).ToArray();var repeated=db.dbo.Items.GroupBy(static row=>row.Id%2).OrderBy(static group=>group.Key,Comparison()).OrderByDescending(static group=>group.Count()).ToArray();var joined=db.dbo.Items.Join(db.dbo.Items,static row=>row.Id%2,static row=>row.Id%2,static(left,right)=>left.N+right.N).OrderByDescending(static value=>value).ThenBy(static value=>value%2).ToArray();var groupCounts=db.dbo.Items.GroupJoin(db.dbo.Items,static row=>row.Id%2,static row=>row.Id%2,static (row,group)=>group.Count()).OrderBy(static value=>value).ToArray();if(groupCounts.Length!=rows.Length||groupCounts.Sum()!=joined.Length){throw new System.Exception(\"GroupJoin mismatch\");}if(groups.Sum(static group=>group.Sum(static row=>row.N))!=total||arrays.Sum(static group=>group.Sum(static row=>row.N))!=total||ordered[0].Key!=1||ordered.Sum(static group=>group.Sum(static row=>row.N))!=total||repeated[0].Key!=0||repeated.Sum(static group=>group.Sum(static row=>row.N))!=total||joined.Sum()!=total*2){throw new System.Exception(\"Escaping group mismatch: groupSum=\"+groups.Sum(static group=>group.Sum(static row=>row.N))+\"; arraySum=\"+arrays.Sum(static group=>group.Sum(static row=>row.N))+\"; orderedKey=\"+ordered[0].Key+\"; orderedSum=\"+ordered.Sum(static group=>group.Sum(static row=>row.N))+\"; repeatedKey=\"+repeated[0].Key+\"; repeatedSum=\"+repeated.Sum(static group=>group.Sum(static row=>row.N))+\"; joinedSum=\"+joined.Sum()+\"; expected=\"+total);}return total;}public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){yield return MemoryPackSerializer.Serialize(Total((global::"+type+")context.Database));}}";
        await host.Transactions.InstallNativeQueryAsync(new("snapshot-module","Query",[new("Query.cs",source)],[new("n","b","","System.Int32","System.Int32")],["b"]));
        Assert.Equal(300,ReadNative(host,session));Execute(engine,session,"BEGIN TRANSACTION; UPDATE dbo.Items SET N=110 WHERE Id=1");Assert.Equal(310,ReadNative(host,session));Execute(engine,session,"ROLLBACK TRANSACTION");Assert.Equal(300,ReadNative(host,session));
        var installed=Assert.Single(host.Transactions.NativeQueries);
        var artifact=Assert.Single(Directory.EnumerateFiles(Path.Combine(buildData,"generated"),"native-build.json",SearchOption.AllDirectories).Select(path=>JsonSerializer.Deserialize<NativeQueryBuildArtifact>(File.ReadAllText(path))!),candidate=>candidate.Sha256==installed.Manifest.AssemblySha256);
        Assert.Equal("snapshot-module",artifact.Manifest.ModuleId);
        var generated=Directory.EnumerateFiles(Path.Combine(artifact.LogDirectory,"generated"),"*.g.cs",SearchOption.AllDirectories).Select(File.ReadAllText).Where(static text=>text.Contains("provider=DatabaseRows",StringComparison.Ordinal)).ToArray();Assert.NotEmpty(generated);
        Assert.Contains(generated,static text=>text.Contains(".GetQueryRows()",StringComparison.Ordinal)&&text.Contains(".Iterate(ref",StringComparison.Ordinal)&&text.Contains(".CreateResultBuffer",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("ContiguousLookupBuilder",StringComparison.Ordinal)||text.Contains("LookupBuilder",StringComparison.Ordinal)||text.Contains("PartitionedLookupBuilder",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("GroupJoin",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("SortIndexes",StringComparison.Ordinal)&&text.Contains("__orderIndexComparer",StringComparison.Ordinal));
    }
}
