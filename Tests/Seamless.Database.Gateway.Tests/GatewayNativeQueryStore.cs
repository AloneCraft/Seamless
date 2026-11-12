using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Database.Gateway.Tests.RecordRpc;

namespace Seamless.Database.Gateway.Tests.PrebuiltPublication;

internal static class DatabaseGenerator{
    public const string ClientContract="Seamless.Database.Gateway.Tests.PrebuiltPublication.IPrebuiltQueries";
    public const string ClientModule="prebuilt";
    public const string ClientDatabase="fixture";
    public const string ServerImplementation="Seamless.Database.Gateway.Tests.PrebuiltPublication.Queries";
}
public interface IPrebuiltQueries{
    RecordPayload Echo(RecordInput input);
}
internal sealed class Queries(INativeQueryContext context):IPrebuiltQueries{
    public RecordPayload Echo(RecordInput input){
        context.CancellationToken.ThrowIfCancellationRequested();
        if(context.Database is not DatabaseContainer||context.Budget is null){throw new InvalidOperationException("Missing execution context.");}
        return new(input.First,input.Second);
    }
}

public sealed class GatewayNativeQueryStore{
    [Theory,InlineData(false),InlineData(true)]
    public async Task PreparePrebuilt_PublishesInvalidatesAndRecoversWithoutNativeCompilation(bool versioned){
        var root=Root();var compiler=GeneratedAssemblyBuilderTests.Compiler(root,Path.Combine(root,"cache"));var storage=Path.Combine(root,"store");
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536};
        if(versioned){options=GatewayVersionedTransactions.Options();}
        using(var host=await GatewayDatabaseHost.OpenAsync(storage,compiler,options,[new("fixture",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var artifact=Artifact(root,host);await host.Transactions.InstallPrebuiltNativeQueryAsync(artifact);
            File.Delete(artifact.AssemblyPath);File.Delete(artifact.SourceProjectPath);
            Assert.Null(Assert.Single(host.Transactions.NativeQueries).Diagnostic);
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await Check(server.Port,host);
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(882,"fixture");
            Execute(engine,session,"BEGIN TRANSACTION; CREATE TABLE dbo.Cancelled(Id int PRIMARY KEY); ROLLBACK TRANSACTION");
            Assert.Null(Assert.Single(host.Transactions.NativeQueries).Diagnostic);await Check(server.Port,host);
            Execute(engine,session,"CREATE TABLE dbo.Committed(Id int PRIMARY KEY)");
            Assert.Contains("prebuilt",Assert.Single(host.Transactions.NativeQueries).Diagnostic!,StringComparison.OrdinalIgnoreCase);
            await using(var client=new NativeQueryClient("127.0.0.1",server.Port)){
                await Assert.ThrowsAsync<NativeQueryException>(()=>new PrebuiltQueriesClient(client,host.Databases.Resolve("fixture").SchemaFingerprint!).EchoAsync(Input()));
            }
            await server.StopAsync();host.Transactions.Checkpoint();
        }
        using(var disabled=await GatewayDatabaseHost.OpenAsync(storage,compiler,options)){
            Assert.Contains("prebuilt",Assert.Single(disabled.Transactions.NativeQueries).Diagnostic!,StringComparison.OrdinalIgnoreCase);
            var engine=new SqlExecutionEngine(disabled.Databases);using var session=new SessionContext(883,"fixture");
            Execute(engine,session,"CREATE TABLE dbo.AfterRestart(Id int PRIMARY KEY)");
            Assert.Contains("prebuilt",Assert.Single(disabled.Transactions.NativeQueries).Diagnostic!,StringComparison.OrdinalIgnoreCase);
            var artifact=Artifact(Path.Combine(root,"replacement"),disabled);await disabled.Transactions.InstallPrebuiltNativeQueryAsync(artifact);
            Assert.Null(Assert.Single(disabled.Transactions.NativeQueries).Diagnostic);
            using var server=new NativeQueryServer(disabled.Databases,0);server.Start();await Check(server.Port,disabled);
            File.Delete(artifact.AssemblyPath);File.Delete(artifact.SourceProjectPath);await server.StopAsync();disabled.Transactions.Checkpoint();
        }
        using(var reopened=await GatewayDatabaseHost.OpenAsync(storage,compiler,options)){
            using var server=new NativeQueryServer(reopened.Databases,0);server.Start();await Check(server.Port,reopened);await server.StopAsync();
        }
        Assert.Empty(Directory.EnumerateFiles(root,"Native.csproj",SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PreparePrebuilt_RejectsMismatchesAndPreservesThePublishedVersion(){
        var root=Root();var compiler=GeneratedAssemblyBuilderTests.Compiler(root,Path.Combine(root,"cache"));
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(root,"store"),compiler,new(){IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536},[new("fixture",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);
        var artifact=Artifact(root,host);await host.Transactions.InstallPrebuiltNativeQueryAsync(artifact);var published=Assert.Single(host.Transactions.NativeQueries).Manifest;
        var dependency=Assert.Single(artifact.Dependencies);var query=Assert.Single(artifact.Manifest.Queries);var wrong=new string('0',64);
        var invalidSource=JsonNode.Parse(File.ReadAllText(artifact.SourceProjectPath))!.AsObject();invalidSource["Files"]=null;
        var invalidPath=Path.Combine(root,"invalid.json");File.WriteAllText(invalidPath,invalidSource.ToJsonString());
        var invalid=new[]{
            artifact with{Sha256=wrong,Manifest=artifact.Manifest with{AssemblySha256=wrong}},
            artifact with{SourceProjectSha256=wrong},
            artifact with{Manifest=artifact.Manifest with{ProductVersion="wrong-build"}},
            artifact with{Dependencies=[dependency with{SchemaFingerprint="wrong-schema"}]},
            artifact with{Dependencies=[dependency with{Sha256=wrong}]},
            artifact with{Manifest=artifact.Manifest with{Queries=[query with{ResultContract=wrong}]}},
            artifact with{SourceProjectPath=invalidPath,SourceProjectSha256=Hash(invalidPath)},
            artifact with{Manifest=artifact.Manifest with{Queries=null!}},
            artifact with{Dependencies=null!},
            artifact with{Manifest=artifact.Manifest with{Queries=new Oversized<global::Seamless.Database.Client.NativeQueryDefinition>()}},
            artifact with{Dependencies=new Oversized<NativeAssemblyDependency>()}
        };
        foreach(var candidate in invalid){
            await Assert.ThrowsAsync<InvalidDataException>(()=>host.Transactions.InstallPrebuiltNativeQueryAsync(candidate));
            Assert.Equal(published,Assert.Single(host.Transactions.NativeQueries).Manifest);
        }
        var current=Assert.Single(Directory.EnumerateFiles(Path.Combine(root,"store","native"),"manifest.json",SearchOption.AllDirectories));
        var legacy=JsonNode.Parse(File.ReadAllText(current))!.AsObject();Assert.True(legacy.Remove("IsPrebuilt"));
        var id=Path.GetFileName(Path.GetDirectoryName(current)!);var legacyRoot=Path.Combine(root,"legacy");var directory=Path.Combine(legacyRoot,id);Directory.CreateDirectory(directory);
        foreach(var file in new[]{"module.dll","source.json"}){File.Copy(Path.Combine(Path.GetDirectoryName(current)!,file),Path.Combine(directory,file));}
        var legacyPath=Path.Combine(directory,"manifest.json");File.WriteAllText(legacyPath,legacy.ToJsonString());
        var legacyStore=new global::Seamless.Database.Gateway.Hosting.GatewayNativeQueryStore(legacyRoot,compiler,new(1<<20,0));
        using(var read=legacyStore.Read(new(id,Path.Combine(id,"manifest.json"),Hash(legacyPath)))){Assert.False(read.IsPrebuilt);Assert.False(read.Artifact!.IsPrebuilt);}
        using var server=new NativeQueryServer(host.Databases,0);server.Start();await Check(server.Port,host);await server.StopAsync();
        Assert.Empty(Directory.EnumerateFiles(root,"Native.csproj",SearchOption.AllDirectories));
    }

    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root."),"prebuilt-"+Guid.NewGuid().ToString("N"));
    private static NativeQueryBuildArtifact Artifact(string root,GatewayDatabaseHost host){
        var database=((InProcessDatabaseAdapter)host.Databases.Resolve("fixture").Adapter).Database.GetType().Assembly;
        var path=NativePrebuiltPackage.Publish(Path.Combine(root,"input"),new PrebuiltQueriesModule(),[NativeDatabaseAssembly.From("fixture",database)]);
        return NativePrebuiltPackage.Read(path);
    }
    private static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input));}
    private static RecordInput Input(){var node=new RecordNode{Number=-0.0};node.Next=node;return new(1,node,node);}
    private static async Task Check(int port,GatewayDatabaseHost host){
        await using var connection=new NativeQueryClient("127.0.0.1",port);
        var client=new PrebuiltQueriesClient(connection,host.Databases.Resolve("fixture").SchemaFingerprint!);var input=Input();var result=await client.EchoAsync(input);
        Assert.Same(result.First,result.Second);Assert.Same(result.First,result.First.Next);Assert.NotSame(input.First,result.First);Assert.Equal(0,BitConverter.DoubleToInt64Bits(result.First.Number));
    }
    private static void Execute(SqlExecutionEngine engine,SessionContext session,string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var _ in result.Rows){}}}
    private sealed class Oversized<T>:IReadOnlyList<T>{
        public int Count=>1025;
        public T this[int index]=>throw new InvalidOperationException("The count must be checked before reading values.");
        public IEnumerator<T> GetEnumerator()=>throw new InvalidOperationException("The count must be checked before copying values.");
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
