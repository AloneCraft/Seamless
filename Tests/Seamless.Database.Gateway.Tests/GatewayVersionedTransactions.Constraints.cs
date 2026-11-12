using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Theory,Trait("Category","Integration")]
    [InlineData(true,true)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(false,false)]
    public async Task ConstraintDependenciesAbortTheWholeMultiDatabaseTransaction(bool unique,bool firstWins){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;
        var root=Path.Combine(data,"mvcc-constraints-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);Guid committed;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),[ConstraintDefinition(unique),Definition("b")])){
            var engine=new SqlExecutionEngine(host.Databases);using var first=new SessionContext(71,"a");using var second=new SessionContext(72,"a");
            Execute(engine,first,"INSERT b.dbo.Items VALUES(1,100),(2,200)");
            if(unique){Execute(engine,first,"INSERT a.dbo.Items VALUES(1,100),(2,200)");}
            else{Execute(engine,first,"INSERT a.dbo.Parent VALUES(1)");}
            Execute(engine,first,"BEGIN TRANSACTION");Execute(engine,second,"BEGIN TRANSACTION");
            // aでは異なる主キー/表、bでも異なる主キー。単なる同一行競合で成功しない試験にする。
            if(unique){Execute(engine,first,"UPDATE a.dbo.Items SET N=300 WHERE Id=1");Execute(engine,second,"UPDATE a.dbo.Items SET N=300 WHERE Id=2");}
            else{Execute(engine,first,"INSERT a.dbo.Child VALUES(10,1)");Execute(engine,second,"DELETE FROM a.dbo.Parent WHERE Id=1");}
            Execute(engine,first,"UPDATE b.dbo.Items SET N=101 WHERE Id=1");Execute(engine,second,"UPDATE b.dbo.Items SET N=202 WHERE Id=2");
            SessionContext winner,loser;if(firstWins){winner=first;loser=second;}else{winner=second;loser=first;}
            committed=winner.Transaction!.Id;var aborted=loser.Transaction!.Id;Execute(engine,winner,"COMMIT TRANSACTION");
            var failure=Assert.Throws<TransactionCommitException>(()=>Execute(engine,loser,"COMMIT TRANSACTION"));Assert.Equal(TransactionOutcomeKind.Aborted,failure.Outcome);
            Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(committed).Kind);Assert.Equal(TransactionOutcomeKind.Aborted,host.Transactions.GetOutcome(aborted).Kind);
            // 通常競合でdomainを停止せず、別DBに残った私有変更も確定させない。
            Execute(engine,loser,"INSERT b.dbo.Items VALUES(3,300)");VerifyConstraintWinner(engine,loser,unique,firstWins);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options())){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(73,"a");VerifyConstraintWinner(engine,session,unique,firstWins);
            Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(committed).Kind);
        }
    }
    private static DatabaseCatalog ConstraintDefinition(bool unique){
        ColumnCatalog Id()=>new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);
        TableCatalog[] tables;
        if(unique){
            var key=new UniqueKeyCatalog("UQ_N",[new("N",false)]);
            tables=[new("Items",[Id(),new(1,"N",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("PK_Items",[new("Id",false)]),[key])];
        }
        else{tables=[new("Parent",[Id()],new("PK_Parent",[new("Id",false)])),new("Child",[Id(),new(1,"Pid",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("PK_Child",[new("Id",false)]),foreignKeys:[new("FK_Child","dbo","Parent",[("Pid","Id")],"NO_ACTION","NO_ACTION")])];}
        return new("a",[new("dbo",tables)],"Japanese_CI_AS");
    }
    private static void VerifyConstraintWinner(SqlExecutionEngine engine,SessionContext session,bool unique,bool firstWins){
        object?[] other;if(firstWins){other=[101,200,300];}else{other=[100,202,300];}
        Assert.Equal(other,engine.Execute("SELECT N FROM b.dbo.Items ORDER BY Id",session).Rows.Select(static row=>row[0]).ToArray());
        if(unique){object?[] values;if(firstWins){values=[300,200];}else{values=[100,300];}Assert.Equal(values,engine.Execute("SELECT N FROM a.dbo.Items ORDER BY Id",session).Rows.Select(static row=>row[0]).ToArray());}
        else{
            var expected=0;if(firstWins){expected=1;}
            Assert.Equal(expected,engine.Execute("SELECT Id FROM a.dbo.Parent",session).Rows.Count());Assert.Equal(expected,engine.Execute("SELECT Id FROM a.dbo.Child",session).Rows.Count());
            if(firstWins){Assert.Equal(new object?[]{10,1},Assert.Single(engine.Execute("SELECT Id,Pid FROM a.dbo.Child",session).Rows));}
        }
    }
}
