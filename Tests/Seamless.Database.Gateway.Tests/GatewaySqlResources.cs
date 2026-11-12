using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewaySqlResources{
    [Fact,Trait("Category","Integration")]
    public async Task SqlExpressionResultsShareTheGatewayBudget(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"sql-resources-"+Guid.NewGuid().ToString("N"));var budget=new ResourceBudget(128L<<20,256L<<20);
        using(var host=await GatewayDatabaseHost.OpenAsync(root,GeneratedAssemblyBuilderTests.Compiler(data,cache),new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")],budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(992,"a");
            using(var memory=budget.ReserveMemory(budget.MemoryLimitBytes-budget.MemoryBytes-(1<<20)))using(var storage=budget.ReserveTemporaryStorage(budget.TemporaryStorageLimitBytes-budget.TemporaryStorageBytes-65536)){
                Assert.Throws<ResourceLimitException>(()=>engine.Execute("SELECT REPLICATE(CAST(N'x' AS nvarchar(max)),2000000)",session).Rows.ToArray());
            }
            Assert.Equal(1,Assert.Single(engine.Execute("SELECT 1",session).Rows)[0]);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
}
