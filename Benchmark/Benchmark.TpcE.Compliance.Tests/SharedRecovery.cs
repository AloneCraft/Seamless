using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class SharedRecovery{
    [Theory,InlineData("Persisted"),InlineData("Tsavorite"),Trait("Category","Integration")]public async Task NativeCommitReplaysFromCheckpointAfterRepeatedInterruptedRecovery(string backend){
        using var lifetime=NativeTestProgress.Measure("recovery-"+backend);
        var flat=EGenFixture.Generate(1000,1);Assert.NotNull(flat);
        var root=Path.Combine(Path.GetTempPath(),"Seamless.C12.NativeRecovery",Guid.NewGuid().ToString("N"));
        var scale=new TpcEScale{Customers=1000,InitialTradeDays=1,MarketDays=1305,NewsItemBytes=100000,TsavoriteSegmentBytes=NativeTestProgress.SegmentBytes};
        ITpcEVariant variant;
        if(backend=="Tsavorite"){variant=new global::Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant();}
        else{variant=new global::Benchmark.TpcE.Persisted.FLinq.PersistedFLinqVariant();}
        var dll=Path.Combine(Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD")??@"G:\GitHub\Seamless\Work\TpcE\egen-build","EGenHarness.dll");
        DatabaseDigest? expected=null;Guid id;
        try{
            using(NativeTestProgress.Measure("recovery-"+backend+"-initial-lifetime"))
            using(var store=Store.Open(Path.Combine(root,"store")))
            using(var engine=variant.Open(store,scale)){
                using(NativeTestProgress.Measure("recovery-"+backend+"-load")){engine.Database.BeginBulkLoad();new Compliance.EGenFlatFileImporter(flat!).Import(engine.Loader);engine.Database.EndBulkLoad();}
                using var transactionPhase=NativeTestProgress.Measure("recovery-"+backend+"-wal-commit-and-digest");
                await using var sut=new Compliance.TpcETransactionalSut(engine,scale,Path.Combine(root,"wal"),dll);
                var database=engine.Database;var account=database.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).First();
                var customer=database.Customer.Get(new CustomerKey(account.CustomerId));var security=database.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First();var company=database.Company.Get(new CompanyKey(security.CompanyId));
                byte[] Request(bool rollback)=>Compliance.HarnessRequest.TradeOrder(account.Id,customer.FirstName,customer.LastName,customer.TaxId.ToString(),company.Name,security.Issue.ToString(),security.Symbol.ToString(),25,"TMB",rollback)[5..];
                var commit=await sut.ExecuteHarnessAsync(HarnessTransaction.TradeOrder,Request(false));id=commit.TransactionId;
                Assert.Equal(0,commit.Result.Status);Assert.NotEmpty(commit.Result.MarketRequests);expected=StateDigest.Of(database);
                var rollback=await sut.ExecuteHarnessAsync(HarnessTransaction.TradeOrder,Request(true));Assert.True(rollback.Result.RolledBack);Assert.Empty(rollback.Result.MarketRequests);Assert.Equal(commit.FenceLsn,rollback.FenceLsn);
                Assert.Empty(expected.Differences(StateDigest.Of(database)));
                // Storeを保存せず閉じ、WALだけを確定済みとする。
            }
            for(var attempt=0;attempt<3;attempt++){
                using var attemptPhase=NativeTestProgress.Measure("recovery-"+backend+"-reopen-"+attempt);
                using var store=Store.Open(Path.Combine(root,"store"));using var engine=variant.Open(store,scale);
                await using var sut=new Compliance.TpcETransactionalSut(engine,scale,Path.Combine(root,"wal"),dll);
                using(NativeTestProgress.Measure("recovery-"+backend+"-digest-"+attempt)){Assert.Empty(expected!.Differences(StateDigest.Of(engine.Database)));}
                Assert.Equal(TransactionOutcomeKind.Committed,sut.GetTransactionOutcome(id).Kind);
                if(attempt>0){using var checkpointPhase=NativeTestProgress.Measure("recovery-"+backend+"-checkpoint-"+attempt);await sut.CheckpointAsync();}
                NativeTestProgress.StoreFiles(backend,attempt,store);
                // 初回の復旧後もcheckpointせず閉じ、live状態への二重適用を防ぐことを確認する。
            }
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}if(flat is not null){EGenFixture.Delete(flat);}}
    }
}
