using Seamless.Transactions;
namespace Seamless.Transactions.Tests;

public sealed partial class OptimisticTransactionTests{
    private static TransactionCoordinator Coordinator(TransactionJournal? journal=null)=>new(journal,null,null,new TransactionCoordinatorOptions{
        Concurrency=TransactionConcurrencyMode.Optimistic,
        History=new(){MaxRetainedMemoryBytes=512<<10,MaxRetainedStorageBytes=2<<20,MaxMetadataBytes=256<<10,
            MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(2)}
    });

    private static Task OutsideCurrent(Func<Task> action){
        using(ExecutionContext.SuppressFlow()){return Task.Run(action);}
    }

    [Fact]
    public async Task PreparedWalWaitAllowsAnotherWriterToReadComputeAndStageAndSnapshotsStayUnpublished(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"mvcc-wal-overlap-"+Guid.NewGuid().ToString("N"));
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var paused=0;
        await using var journal=new PreparedTransactionJournal(path,new(){CatalogIdentity="mvcc-wal-overlap",MaxFrameBytes=1024,QueueBytes=4096,SegmentBytes=16384},failureInjection:phase=>{
            if(phase==PreparedTransactionPhase.PreparationDurable&&Interlocked.CompareExchange(ref paused,1,0)==0){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}}
        });
        await using var owner=new TransactionCoordinator(null,null,journal,Options());var left=new Versions(1);var right=new Versions(2);owner.Register(left);owner.Register(right);
        var first=OutsideCurrent(async()=>{await using var tx=await owner.BeginAsync();left.Set(tx,1,90);await tx.CommitAsync();});Task? second=null;
        try{
            Assert.True(await Task.Run(()=>entered.Wait(TimeSpan.FromSeconds(5))));Assert.False(first.IsCompleted);Assert.Equal(0,owner.GetDomainDiagnostics().PublishedVersion);
            var staged=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            second=OutsideCurrent(async()=>{await using var tx=await owner.BeginAsync();var value=right.Read(tx,1)!.Value;right.Set(tx,1,value-20);staged.TrySetResult();await tx.CommitAsync();});
            await staged.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(first.IsCompleted);
            await using var snapshot=await owner.BeginReadOnlyAsync();Assert.Equal(100,left.Read(snapshot,1));Assert.Equal(100,right.Read(snapshot,1));
            release.Set();await Task.WhenAll(first,second).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(100,left.Read(snapshot,1));Assert.Equal(100,right.Read(snapshot,1));Assert.Equal(90,left.Current(1));Assert.Equal(80,right.Current(1));
        }finally{release.Set();await first.WaitAsync(TimeSpan.FromSeconds(10));if(second is not null){await second.WaitAsync(TimeSpan.FromSeconds(10));}}
        Assert.Null(owner.Failure.Error);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);
    }
    [Fact]
    public async Task DistinctWritersOverlapWithoutExposingTheirStagedChanges(){
        await using var owner=Coordinator();
        var left=new Versions(1);var right=new Versions(2);
        owner.Register(left);owner.Register(right);
        await using(var first=await owner.BeginAsync()){
            Assert.Equal(100,left.Read(first,1));
            left.Set(first,1,90);
            Assert.Equal(100,left.Current(1));
            await OutsideCurrent(async()=>{
                await using var second=await owner.BeginAsync();
                right.Set(second,1,80);
                Assert.Equal(TransactionCommitKind.Committed,(await second.CommitAsync()).Kind);
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(100,left.Current(1));
            Assert.Equal(80,right.Current(1));
            Assert.Equal(90,left.Read(first,1));
            Assert.Equal(TransactionCommitKind.Committed,(await first.CommitAsync()).Kind);
        }
        await using var verify=await owner.BeginReadOnlyAsync();
        Assert.Equal(90,left.Read(verify,1));Assert.Equal(80,right.Read(verify,1));
    }

    [Fact]
    public async Task ConflictingWriterAbortsWithoutFaultingFollowingTransactions(){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);
        await using(var first=await owner.BeginAsync()){
            Assert.Equal(100,table.Read(first,1));table.Set(first,1,90);
            await OutsideCurrent(async()=>{
                await using var second=await owner.BeginAsync();table.Set(second,1,80);await second.CommitAsync();
            }).WaitAsync(TimeSpan.FromSeconds(5));
            var conflict=await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await first.CommitAsync());
            Assert.Equal(TransactionAbortReason.Conflict,conflict.Reason);
            Assert.Equal(first.Id,conflict.TransactionId);
        }
        Assert.Null(owner.Failure.Error);
        await using var next=await owner.BeginAsync();Assert.Equal(80,table.Read(next,1));table.Set(next,1,70);await next.CommitAsync();
        Assert.Equal(70,table.Current(1));
    }

    [Fact]
    public async Task ReadOnlySnapshotSurvivesDeletionAndReinsertion(){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);
        await using(var old=await owner.BeginReadOnlyAsync()){
            Assert.Equal(100,table.Read(old,1));
            await OutsideCurrent(async()=>{
                await using(var remove=await owner.BeginAsync()){table.Delete(remove,1);await remove.CommitAsync();}
                await using(var insert=await owner.BeginAsync()){table.Set(insert,1,200);await insert.CommitAsync();}
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(100,table.Read(old,1));
            Assert.Equal(TransactionCommitKind.NoChanges,(await old.CommitAsync()).Kind);
        }
        await using var current=await owner.BeginReadOnlyAsync();Assert.Equal(200,table.Read(current,1));
    }

    [Fact]
    public async Task WriteSkewAcrossTwoTablesAbortsTheOlderSnapshot(){
        await using var owner=Coordinator();var left=new Versions(1);var right=new Versions(2);owner.Register(left);owner.Register(right);
        await using var first=await owner.BeginAsync();Assert.Equal(100,right.Read(first,1));left.Set(first,1,0);
        await OutsideCurrent(async()=>{
            await using var second=await owner.BeginAsync();Assert.Equal(100,left.Read(second,1));right.Set(second,1,0);await second.CommitAsync();
        }).WaitAsync(TimeSpan.FromSeconds(5));
        var error=await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await first.CommitAsync());Assert.Equal(TransactionAbortReason.Conflict,error.Reason);
        Assert.Equal(100,left.Current(1));Assert.Equal(0,right.Current(1));Assert.Null(owner.Failure.Error);
    }

    [Fact]
    public async Task MissingKeysAndWholeTableScansAreDependencies(){
        foreach(var scan in new[]{false,true}){
            await using var owner=Coordinator();var input=new Versions(1);var output=new Versions(2);owner.Register(input);owner.Register(output);
            await using var first=await owner.BeginAsync();
            if(scan){Assert.Equal(1,input.Count(first));}else{Assert.Null(input.Read(first,2));}
            await OutsideCurrent(async()=>{await using var second=await owner.BeginAsync();input.Set(second,2,200);await second.CommitAsync();});
            output.Set(first,1,50);
            Assert.Equal(TransactionAbortReason.Conflict,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await first.CommitAsync())).Reason);
            Assert.Equal(100,output.Current(1));
        }
    }

    [Fact]
    public async Task SavepointRollbackKeepsReadDependencies(){
        await using var owner=Coordinator();var input=new Versions(1);var output=new Versions(2);owner.Register(input);owner.Register(output);
        await using var first=await owner.BeginAsync();first.CreateSavepoint("before-read");Assert.Equal(100,input.Read(first,1));first.RollbackToSavepoint("before-read");
        await OutsideCurrent(async()=>{await using var second=await owner.BeginAsync();input.Set(second,1,200);await second.CommitAsync();});
        output.Set(first,1,50);Assert.Equal(TransactionAbortReason.Conflict,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await first.CommitAsync())).Reason);
    }

    [Fact]
    public async Task SnapshotStartsBeforeAllParticipantInstallationFinishes(){
        await using var owner=Coordinator();var left=new Versions(1);var right=new Versions(2);owner.Register(left);owner.Register(right);
        using var halfway=new ManualResetEventSlim();using var finish=new ManualResetEventSlim();
        right.BeforeInstall=()=>{halfway.Set();if(!finish.Wait(TimeSpan.FromSeconds(5))){throw new TimeoutException();}};
        var commit=OutsideCurrent(async()=>{await using var writer=await owner.BeginAsync();left.Set(writer,1,90);right.Set(writer,1,80);await writer.CommitAsync();});
        try{
            Assert.True(halfway.Wait(TimeSpan.FromSeconds(5)));
            await using var old=await owner.BeginReadOnlyAsync();Assert.Equal(100,left.Read(old,1));Assert.Equal(100,right.Read(old,1));
            finish.Set();await commit.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(100,left.Read(old,1));Assert.Equal(100,right.Read(old,1));
        }finally{finish.Set();await commit;}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DurableReplayAssignsVersionsAboveCheckpointRows(bool prepared){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,Guid.NewGuid().ToString("N"));
        var options=new WalOptions{CatalogIdentity="mvcc-replay",MaxFrameBytes=16<<10,QueueBytes=64<<10};
        Guid committed;
        async Task Write(){
            await using var journal=CreateJournal();await using var preparedJournal=CreatePrepared();
            await using var owner=new TransactionCoordinator(journal,null,preparedJournal,Options());var table=new Versions(1);owner.Register(table);
            await using var tx=await owner.BeginAsync();committed=tx.Id;table.Set(tx,1,70);await tx.CommitAsync();
            Assert.Equal(TransactionOutcomeKind.Committed,(await owner.GetOutcomeAsync(tx.Id)).Kind);
        }
        TransactionJournal? CreateJournal(){if(prepared){return null;}return new(path,options);}
        PreparedTransactionJournal? CreatePrepared(){if(prepared){return new(path,options);}return null;}
        committed=Guid.Empty;await Write();
        await using var journal=CreateJournal();await using var preparedJournal=CreatePrepared();
        await using var recovered=new TransactionCoordinator(journal,null,preparedJournal,Options());var restored=new Versions(1);restored.SeedVersion(50);recovered.Register(restored);
        await using var verify=await recovered.BeginReadOnlyAsync();Assert.Equal(70,restored.Read(verify,1));Assert.True(restored.RecoveredVersion>50);
        Assert.Equal(TransactionOutcomeKind.Committed,(await recovered.GetOutcomeAsync(committed)).Kind);
    }

    private static TransactionCoordinatorOptions Options()=>new(){Concurrency=TransactionConcurrencyMode.Optimistic,
        History=new(){MaxRetainedMemoryBytes=512<<10,MaxRetainedStorageBytes=2<<20,MaxMetadataBytes=256<<10,
            MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(2)}};

    [Fact]
    public async Task CancellationBeforeAcceptanceAbortsOnlyThatTransaction(){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);using var cancellation=new CancellationTokenSource();
        await using(var tx=await owner.BeginAsync()){
            table.Set(tx,1,70);cancellation.Cancel();
            var error=await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await tx.CommitAsync(ReadOnlyMemory<byte>.Empty,cancellation.Token));
            Assert.Equal(TransactionAbortReason.CallerCancellation,error.Reason);Assert.Equal(TransactionOutcomeKind.Aborted,(await owner.GetOutcomeAsync(tx.Id)).Kind);
        }
        Assert.Equal(100,table.Current(1));Assert.Null(owner.Failure.Error);
        await using var next=await owner.BeginAsync();table.Set(next,1,80);await next.CommitAsync();Assert.Equal(80,table.Current(1));
    }

    [Fact]
    public async Task CancellationAfterDurabilityDoesNotUndoTheAcceptedCommit(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,Guid.NewGuid().ToString("N"));
        await using var journal=new TransactionJournal(path,new WalOptions{CatalogIdentity="mvcc-cancel",MaxFrameBytes=16<<10,QueueBytes=64<<10});
        await using var owner=Coordinator(journal);var table=new Versions(1);owner.Register(table);using var cancellation=new CancellationTokenSource();table.BeforeInstall=cancellation.Cancel;
        await using var tx=await owner.BeginAsync();table.Set(tx,1,70);var result=await tx.CommitAsync(ReadOnlyMemory<byte>.Empty,cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);Assert.Equal(TransactionCommitKind.Committed,result.Kind);Assert.Equal(70,table.Current(1));
        Assert.Equal(TransactionOutcomeKind.Committed,(await owner.GetOutcomeAsync(tx.Id)).Kind);Assert.Null(owner.Failure.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterDurabilityCannotPublishPastAnEarlierIncompleteInstallation(bool prepared){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,Guid.NewGuid().ToString("N"));
        var options=new WalOptions{CatalogIdentity="mvcc-prefix",MaxFrameBytes=16<<10,QueueBytes=64<<10};
        TransactionJournal? plain=null;PreparedTransactionJournal? chunks=null;
        if(prepared){chunks=new(path,options);}else{plain=new(path,options);}
        await using var journal=plain;await using var preparedJournal=chunks;
        await using var owner=new TransactionCoordinator(plain,null,chunks,Options());var left=new Versions(1);var right=new Versions(2);var other=new Versions(3);owner.Register(left);owner.Register(right);owner.Register(other);
        using var halfway=new ManualResetEventSlim();using var finish=new ManualResetEventSlim();
        right.BeforeInstall=()=>{halfway.Set();if(!finish.Wait(TimeSpan.FromSeconds(5))){throw new TimeoutException();}};
        var first=OutsideCurrent(async()=>{await using var tx=await owner.BeginAsync();left.Set(tx,1,90);right.Set(tx,1,80);await tx.CommitAsync();});
        Task? second=null;
        try{
            Assert.True(halfway.Wait(TimeSpan.FromSeconds(5)));var durable=plain?.Writer.DurableLsn??chunks!.DurableLsn;
            second=OutsideCurrent(async()=>{await using var tx=await owner.BeginAsync();other.Set(tx,1,70);await tx.CommitAsync();});
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while((plain?.Writer.DurableLsn??chunks!.DurableLsn)<=durable&&timer.Elapsed<TimeSpan.FromSeconds(3)){await Task.Delay(10);}
            Assert.True((plain?.Writer.DurableLsn??chunks!.DurableLsn)>durable);Assert.False(second.IsCompleted);Assert.Equal(0,owner.GetDomainDiagnostics().PublishedVersion);
            await using var snapshot=await owner.BeginReadOnlyAsync();Assert.Equal(100,left.Read(snapshot,1));Assert.Equal(100,right.Read(snapshot,1));Assert.Equal(100,other.Read(snapshot,1));
            finish.Set();await Task.WhenAll(first,second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(100,other.Read(snapshot,1));Assert.Equal(70,other.Current(1));
        }finally{finish.Set();await first;if(second is not null){await second;}}
    }

    [Fact]
    public async Task ReclamationWaitsForTheExecutingReadAndRetiresTheVictimWithoutDispose(){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var old=await owner.BeginReadOnlyAsync();
        table.BeforeRead=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(5))){throw new TimeoutException();}};
        var read=Task.Run(()=>table.Read(old,1));Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var reclaim=OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());
        var timer=System.Diagnostics.Stopwatch.StartNew();while(!owner.GetDomainDiagnostics().Reclaiming&&timer.Elapsed<TimeSpan.FromSeconds(2)){await Task.Delay(10);}
        Assert.True(owner.GetDomainDiagnostics().Reclaiming);Assert.Equal(0,table.Reclaims);
        using var cancelled=new CancellationTokenSource();
        var waiting=OutsideCurrent(async()=>{await using var tx=await owner.BeginReadOnlyAsync(cancelled.Token);});
        Assert.False(waiting.IsCompleted);cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await waiting);
        release.Set();Assert.Equal(TransactionAbortReason.HistoryPressure,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await read)).Reason);
        await reclaim.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(1,table.Reclaims);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);
        Assert.Equal(TransactionAbortReason.HistoryPressure,Assert.Throws<TransactionAbortedException>(()=>table.Read(old,1)).Reason);
        await old.DisposeAsync();Assert.Null(owner.Failure.Error);
    }

    [Fact]
    public async Task MetadataExhaustionAbortsOnlyTheOwningTransaction(){
        var options=Options();options=options with{History=options.History! with{MaxMetadataBytes=4096}};
        await using var owner=new TransactionCoordinator(null,null,null,options);var table=new Versions(1);owner.Register(table);
        await using(var tx=await owner.BeginAsync()){
            var error=Assert.Throws<TransactionAbortedException>(()=>{for(var key=0;key<20;key++){table.Read(tx,key);}});Assert.Equal(TransactionAbortReason.ResourceLimit,error.Reason);
        }
        Assert.Null(owner.Failure.Error);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);
        await using var next=await owner.BeginReadOnlyAsync();Assert.Equal(100,table.Read(next,1));
    }

    [Fact]
    public async Task ReadOnlyParticipantStateAlsoHasAFiniteMetadataBudget(){
        var options=Options();options=options with{History=options.History! with{MaxMetadataBytes=4096}};
        await using var owner=new TransactionCoordinator(null,null,null,options);var tables=Enumerable.Range(1,6).Select(static id=>new Versions(id)).ToArray();foreach(var table in tables){owner.Register(table);}
        await using(var tx=await owner.BeginReadOnlyAsync()){
            Assert.Equal(TransactionAbortReason.ResourceLimit,Assert.Throws<TransactionAbortedException>(()=>{foreach(var table in tables){table.Read(tx,1);}}).Reason);
        }
        Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(owner.Failure.Error);
        await using var next=await owner.BeginReadOnlyAsync();Assert.Equal(100,tables[0].Read(next,1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavepointsAndJoinedScopesCannotGrowMetadataWithoutABound(bool nested){
        var options=Options();options=options with{History=options.History! with{MaxMetadataBytes=4096}};
        await using var owner=new TransactionCoordinator(null,null,null,options);var table=new Versions(1);owner.Register(table);
        await using(var tx=await owner.BeginReadOnlyAsync()){
            table.Read(tx,1);var current=tx;var children=new List<TransactionScope>();
            try{
                var error=Assert.Throws<TransactionAbortedException>(()=>{
                    for(var index=0;index<100;index++){
                        if(nested){current=current.BeginNested();children.Add(current);}else{tx.CreateSavepoint("point-"+index);}
                    }
                });Assert.Equal(TransactionAbortReason.ResourceLimit,error.Reason);
            }finally{foreach(var child in children){await child.DisposeAsync();}}
        }
        Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(owner.Failure.Error);
        await using var next=await owner.BeginReadOnlyAsync();Assert.Equal(100,table.Read(next,1));
    }

    private sealed class Versions(int tableId):IReclaimableTransactionParticipant{
        private readonly object gate=new();
        private readonly Dictionary<int,List<(long Version,long? Value)>> rows=new(){{1,[(0,100)]}};
        internal Action? BeforeInstall;
        internal Action? BeforeRead;
        internal Action? BeforeReopen;
        internal Action? BeforeReclaim;
        internal int MaintenanceFaults;
        public void ReopenAfterMaintenance()=>this.BeforeReopen?.Invoke();
        public void FaultMaintenance(Exception error)=>Interlocked.Increment(ref this.MaintenanceFaults);
        internal int Reclaims;
        public ValueTask ReclaimAsync(){this.BeforeReclaim?.Invoke();lock(this.gate){foreach(var values in this.rows.Values){var current=values[^1];values.Clear();values.Add(current);}this.Reclaims++;}return ValueTask.CompletedTask;}
        internal void SeedVersion(long version){lock(this.gate){this.rows[1]=[(version,100)];}}
        public int Partition=>0;
        public int TableId=>tableId;
        public string CodecIdentity=>"versioned-test-v1";
        public long RecoveredVersion{get{lock(this.gate){return this.rows.Values.SelectMany(static values=>values).Max(static item=>item.Version);}}}
        public ITransactionChanges CreateChanges()=>throw new InvalidOperationException("A versioned context is required.");
        public IVersionedTransactionChanges CreateChanges(TransactionContext context)=>new Changes(this,context);
        public void Replay(WalEntry entry)=>throw new InvalidOperationException("Use versioned replay.");
        public void Replay(WalEntry entry,TransactionInstallationContext context){
            long? value=null;
            if(entry.Mutation!=WalMutation.Delete){value=BitConverter.ToInt64(entry.Row!);}
            this.Install(BitConverter.ToInt32(entry.Key),value,context.CommitVersion);
        }
        public long? Read(TransactionScope tx,int key)=>tx.Use<long?,Changes>(this,changes=>changes.Read(key));
        public void Set(TransactionScope tx,int key,long value)=>tx.Use<bool,Changes>(this,changes=>{changes.Set(key,value);return true;});
        public void Delete(TransactionScope tx,int key)=>tx.Use<bool,Changes>(this,changes=>{changes.Set(key,null);return true;});
        public int Count(TransactionScope tx)=>tx.Use<int,Changes>(this,changes=>changes.Count());
        public long? Current(int key)=>this.At(key,long.MaxValue);
        private long? At(int key,long version){
            lock(this.gate){
                if(this.rows.TryGetValue(key,out var values)){
                    for(var index=values.Count-1;index>=0;index--){if(values[index].Version<=version){return values[index].Value;}}
                }
                return null;
            }
        }
        private void Install(int key,long? value,long version){
            lock(this.gate){
                if(!this.rows.TryGetValue(key,out var values)){values=[];this.rows.Add(key,values);}
                values.Add((version,value));
            }
        }
        private sealed class Preparation(Versions table,KeyValuePair<int,long?>[] rows):PreparedTransactionParticipant{
            internal KeyValuePair<int,long?>[] Rows{get;}=rows;
            public override long MutationCount=>this.Rows.LongLength;
            public override IEnumerable<WalEntry> Entries{
                get{
                    foreach(var row in this.Rows){
                        byte[]? value=null;var mutation=WalMutation.Delete;
                        if(row.Value.HasValue){value=BitConverter.GetBytes(row.Value.Value);mutation=WalMutation.Upsert;}
                        yield return new(table.Partition,table.TableId,mutation,table.CodecIdentity,BitConverter.GetBytes(row.Key),value);
                    }
                }
            }
            public override void Dispose(){}
        }
        private sealed class Changes(Versions table,TransactionContext context):IVersionedTransactionChanges{
            private readonly Dictionary<int,long?> writes=[];
            public long? Read(int key){
                context.ThrowIfUnavailable();context.ObserveRead(table,BitConverter.GetBytes(key));
                table.BeforeRead?.Invoke();
                if(this.writes.TryGetValue(key,out var own)){return own;}
                return table.At(key,context.ReadVersion);
            }
            public void Set(int key,long? value){context.EnsureWritable();context.ObserveWrite(table,BitConverter.GetBytes(key));this.writes[key]=value;}
            public int Count(){context.ObserveTableRead(table);lock(table.gate){return table.rows.Keys.Count(key=>this.Read(key).HasValue);}}
            public ITransactionChanges CreateSnapshot(){var copy=new Changes(table,context);foreach(var pair in this.writes){copy.writes.Add(pair.Key,pair.Value);}return copy;}
            public IReadOnlyList<WalEntry> Encode()=>new Preparation(table,this.writes.ToArray()).Entries.ToArray();
            public void Apply()=>throw new InvalidOperationException("Versioned installation is required.");
            public ValueTask<PreparedTransactionParticipant> PrepareAsync(TransactionPreparationContext preparation,CancellationToken cancellationToken){
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult<PreparedTransactionParticipant>(new Preparation(table,this.writes.ToArray()));
            }
            public void Install(TransactionInstallationContext installation,PreparedTransactionParticipant preparation){
                table.BeforeInstall?.Invoke();
                foreach(var row in ((Preparation)preparation).Rows){table.Install(row.Key,row.Value,installation.CommitVersion);}
            }
            public void Dispose(){}
        }
    }
}
