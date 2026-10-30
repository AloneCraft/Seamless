using Benchmark.TpcC.Core;
using Benchmark.Tpc.Core;
using Benchmark.TpcC.Server;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
public sealed class TpcCPartitionedSutTests{
    [Theory][InlineData(1)][InlineData(2)]public async Task FourVariantsRecoverAfterPartialCheckpoint(int partitions){
        foreach(var variant in TestDatabases.Variants){
            var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.TpcC",Guid.NewGuid().ToString("N"));var scale=TestDatabases.Mini(2,partitions);
            DatabaseDigest? expected=null;Guid id;
            Store[] Open()=>Enumerable.Range(0,partitions).Select(part=>Store.Open(Path.Combine(path,"p"+part))).ToArray();
            try{
                await using(var sut=new Server.TpcCPartitionedSut(variant,Open(),scale,TpcCMode.Spec,Path.Combine(path,"wal"),engine=>{
                    engine.Database.BeginBulkLoad();new Core.TpcCDataGenerator(scale,TpcCLoadRules.For(TpcCMode.Spec),1).Generate(engine.Loader);engine.Database.EndBulkLoad();
                })){
                    var response=await sut.ExecuteAsync(new PaymentRequest(1,123,1,2,1,1,null,10m));id=response.TransactionId;
                    Assert.Equal(TransactionOutcomeKind.Committed,sut.GetTransactionOutcome(id).Kind);
                    var rollback=await sut.ExecuteAsync(new NewOrderRequest(1,124,1,1,[new(1,2,5),new(scale.Items+1,1,1)],0));Assert.True(rollback.Result.RolledBack);
                    var read=await sut.ExecuteAsync(new StockLevelRequest(2,125,1,20));Assert.Equal(response.FenceLsn,read.FenceLsn);
                    if(partitions>1){await sut.CheckpointAsync(0);}expected=Core.TpcCStateDigest.Of(sut.Engine.Database);
                }
                var appliedBeforeStop=0;
                Assert.Throws<IOException>(()=>new Server.TpcCPartitionedSut(variant,Open(),scale,TpcCMode.Spec,Path.Combine(path,"wal"),replayApplied:entry=>{
                    appliedBeforeStop++;throw new IOException("Injected stop after a replay mutation, before checkpoint.");
                }));
                Assert.Equal(1,appliedBeforeStop);
                await using(var recovered=new Server.TpcCPartitionedSut(variant,Open(),scale,TpcCMode.Spec,Path.Combine(path,"wal"))){
                    Assert.Empty(expected!.Differences(Core.TpcCStateDigest.Of(recovered.Engine.Database)));
                    Assert.Equal(TransactionOutcomeKind.Committed,recovered.GetTransactionOutcome(id).Kind);
                    Assert.Empty(TpcCIndexConsistency.Check(recovered.Engine.Database));
                }
            }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
        }
    }
    [Fact]public void ExpandedInputCodecPreservesAllFields(){
        var request=new NewOrderRequest(1,9876,2,3,[new(10,2,4)],0);
        var envelope=new TpcCReplayEnvelope(1,TpcCMode.Spec,request.Kind,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request),[],[]);
        var decoded=Assert.IsType<NewOrderRequest>(envelope.DecodeRequest());Assert.Equal(request.Now,decoded.Now);Assert.Equal(request.Lines,decoded.Lines);Assert.Equal(request.AllLocal,decoded.AllLocal);
    }
    [Fact]public async Task AuditModeRecordsReadsAndRollbackAndRejectsNormalModeReuse(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Audit",Guid.NewGuid().ToString("N"));var scale=TestDatabases.Mini(1,1);
        try{
            await using(var sut=new Server.TpcCPartitionedSut(TestDatabases.Variants[0],[Store.Open(Path.Combine(path,"store"))],scale,TpcCMode.Spec,Path.Combine(path,"wal"),engine=>{
                engine.Database.BeginBulkLoad();new Core.TpcCDataGenerator(scale,TpcCLoadRules.For(TpcCMode.Spec),1).Generate(engine.Loader);engine.Database.EndBulkLoad();
            },auditMode:true)){
                var read=await sut.ExecuteAsync(new StockLevelRequest(1,123,1,20));Assert.Equal(1,read.FenceLsn);
                var rollback=await sut.ExecuteAsync(new NewOrderRequest(1,124,1,1,[new(1,1,1),new(scale.Items+1,1,1)],0));Assert.True(rollback.Result.RolledBack);Assert.Equal(2,rollback.FenceLsn);
            }
            Assert.Throws<InvalidDataException>(()=>new Server.TpcCPartitionedSut(TestDatabases.Variants[0],[Store.Open(Path.Combine(path,"store"))],scale,TpcCMode.Spec,Path.Combine(path,"wal")));
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
}
