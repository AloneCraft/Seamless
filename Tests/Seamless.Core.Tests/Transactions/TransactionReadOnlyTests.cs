using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
namespace Seamless.Transactions.Tests;

public sealed class TransactionReadOnlyTests{
    [Fact]
    public async Task ReadOnlyScopeRejectsEveryMutationAndSharesItsContextWithChildren(){
        using var resources=new ExecutionScope();
        await using var owner=new TransactionCoordinator();
        var data=new Dictionary<int,(int Id,long Value)>{{1,(1,100)}};
        var table=new TransactionalTable<int,(int Id,long Value)>(0,1,"read-only-test",resources,1024,
            key=>{var found=data.TryGetValue(key,out var row);return(found,row);},
            (key,row)=>data[key]=row,key=>data.Remove(key),static row=>row.Id,()=>data);
        owner.Register(table);
        TransactionCollection<int,(int Id,long Value)> retained;
        await using(var tx=await owner.BeginReadOnlyAsync()){
            Assert.True(tx.IsReadOnly);
            var rows=table.For(tx);
            Assert.Equal(100,rows[1].Value);
            Assert.Throws<InvalidOperationException>(()=>rows[1]=(1,50));
            Assert.Throws<InvalidOperationException>(()=>rows.Add((1,50)));
            Assert.Throws<InvalidOperationException>(()=>rows.Remove(999));
            retained=rows;
            tx.CreateSavepoint("read");
            tx.RollbackToSavepoint("read");
            await using(var child=tx.BeginNested()){
                Assert.True(child.IsReadOnly);
                Assert.Equal(tx.Id,child.Id);
                Assert.Equal(100,table.For(child)[1].Value);
                Assert.Equal(TransactionCommitKind.Voted,(await child.CommitAsync()).Kind);
            }
            Assert.Equal(TransactionCommitKind.NoChanges,(await tx.CommitAsync()).Kind);
        }
        Assert.Throws<ObjectDisposedException>(()=>retained[1]);
        Assert.Equal(100,data[1].Value);
        Assert.Null(owner.Failure.Error);
        await using var next=await owner.BeginAsync();
        Assert.False(next.IsReadOnly);
        table.For(next)[1]=(1,90);
        await next.CommitAsync();
        Assert.Equal(90,data[1].Value);
    }

    [Fact]
    public async Task ReadOnlyScopeRejectsUnknownParticipantsAndArbitraryEnlistment(){
        await using var owner=new TransactionCoordinator();
        var participant=new LegacyParticipant();
        owner.Register(participant);
        await using(var tx=await owner.BeginReadOnlyAsync()){
            Assert.Throws<NotSupportedException>(()=>tx.Use<int,ITransactionChanges>(participant,static _=>1));
            Assert.Equal(0,participant.Created);
            using var extra=new LegacyChanges();
            Assert.Throws<InvalidOperationException>(()=>tx.Enlist(extra));
            Assert.False(extra.Applied);
            Assert.Equal(TransactionCommitKind.NoChanges,(await tx.CommitAsync()).Kind);
        }
        Assert.Null(owner.Failure.Error);
        await using var next=await owner.BeginAsync();
        Assert.Equal(1,next.Use<int,ITransactionChanges>(participant,static _=>1));
        Assert.Equal(1,participant.Created);
        await next.CommitAsync();
    }

    private sealed class LegacyParticipant:ITransactionParticipant{
        public int Partition=>0;
        public int TableId=>1;
        public string CodecIdentity=>"legacy-test";
        public int Created{get;private set;}
        public ITransactionChanges CreateChanges(){this.Created++;return new LegacyChanges();}
        public void Replay(WalEntry entry){}
    }

    [Fact]
    public async Task ReadOnlyCommitDoesNotAppendToEitherWalProfile(){
        foreach(var prepared in new[]{false,true}){
            var root=Path.Combine(Path.GetTempPath(),"Seamless.ReadOnlyWal");
            var path=Path.Combine(root,Guid.NewGuid().ToString("N"));
            try{
                TransactionJournal? single=null;
                PreparedTransactionJournal? batch=null;
                if(prepared){batch=new PreparedTransactionJournal(path);}
                else{single=new TransactionJournal(path);}
                await using var journal=single;
                await using var chunks=batch;
                await using var owner=new TransactionCoordinator(journal,preparedJournal:chunks);
                await using(var tx=await owner.BeginReadOnlyAsync()){
                    await Assert.ThrowsAsync<InvalidOperationException>(async()=>await tx.CommitAsync(new byte[]{1}));
                    Assert.Equal(TransactionCommitKind.NoChanges,(await tx.CommitAsync()).Kind);
                }
                if(journal is not null){Assert.Equal(0,journal.Writer.LastAcceptedLsn);}
                if(chunks is not null){Assert.Equal(0,chunks.DurableLsn);}
                Assert.Null(owner.Failure.Error);
                await using var next=await owner.BeginAsync();
                Assert.Equal(TransactionCommitKind.Committed,(await next.CommitAsync(new byte[]{2})).Kind);
            }finally{
                var absolute=Path.GetFullPath(path);
                Assert.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,absolute,StringComparison.OrdinalIgnoreCase);
                if(Directory.Exists(absolute)){Directory.Delete(absolute,true);}
            }
        }
    }

    private sealed class LegacyChanges:ITransactionChanges{
        public bool Applied{get;private set;}
        public IReadOnlyList<WalEntry> Encode()=>[];
        public void Apply()=>this.Applied=true;
        public void Dispose(){}
    }
}
