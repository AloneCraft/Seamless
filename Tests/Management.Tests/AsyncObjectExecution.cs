using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class AsyncObjectExecution{
    [Theory]
    [InlineData("ScalarFunction","global::System.Threading.Tasks.Task<int>")]
    [InlineData("ScalarFunction","global::System.Threading.Tasks.ValueTask<int>")]
    [InlineData("ScalarFunction","global::System.Collections.Generic.IAsyncEnumerable<int>")]
    [InlineData("ScalarFunction","global::System.Collections.Generic.IAsyncEnumerator<int>")]
    [InlineData("TableFunction","global::System.Threading.Tasks.Task<int>")]
    public async Task UnsupportedAsyncCandidatesAreRejectedAndThePublishedSynchronousFunctionRemainsUsable(string wrapper,string type){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"async-object-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var source=$$"""
            using Seamless.Database;
            namespace AsyncFixture;
            internal static partial class DatabaseGenerator;
            public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){
             public global::Seamless.Database.{{wrapper}}<{{type}}> Pending()=>this.{{wrapper}}<{{type}}>(()=>throw new global::System.InvalidOperationException("body was invoked"));
             public global::Seamless.Database.ScalarFunction<int> Safe()=>this.ScalarFunction<int>(()=>42);
            }
            """;
        var definition=new Definition("async-object","AsyncObject",DefinitionKind.Database,"AsyncFixture.Db","",new("1",[new("Database.cs",source)],[]));
        var pending="public global::Seamless.Database."+wrapper+"<"+type+"> Pending()=>this."+wrapper+"<"+type+">(()=>throw new global::System.InvalidOperationException(\"body was invoked\"));";
        Assert.Contains(pending,source);
        var published=definition with{Source=definition.Source with{Files=[new("Database.cs",source.Replace(pending,"",StringComparison.Ordinal))]}};
        var draft=service.SaveDraft(owner,new(published,0,0));var preview=service.Preview(owner,new(definition.Id,draft.Revision));
        Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
        var applied=service.Apply(owner,new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()));Assert.Equal(OperationState.Succeeded,applied.State);
        var rejected=service.SaveDraft(owner,new(definition,draft.Revision,1));var impact=service.Preview(owner,new(definition.Id,rejected.Revision));
        Assert.False(impact.CanApply);Assert.Contains(impact.Diagnostics,item=>item.Message.Contains("対応範囲外の型",StringComparison.Ordinal));
        var result=await service.ExecuteObjectAsync(owner,new("AsyncObject","Safe",[]));Assert.Equal(42,result.Value!.Value.GetInt32());
        Assert.Equal(1,service.GetDefinition(owner,definition.Id).Version);
    }
}
