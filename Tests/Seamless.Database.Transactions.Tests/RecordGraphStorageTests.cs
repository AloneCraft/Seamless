using Seamless.Database;
using Seamless.Transactions;
using Seamless.Transactions.Collections;

namespace Test.Seamless.Database.RecordGraphs;

public sealed class RecordGraphStorageTests{
    [Theory,InlineData(false),InlineData(true)]
    public void ViewContains_UsesNormalizedValuesAndReferenceTopology(bool versioned){
        using var database=new GraphDatabase(Options(versioned));
        object[] first=[-0.0,12.00m,null!];first[2]=first;
        object[] equal=[0.0,12m,null!];equal[2]=equal;
        var evaluations=0;
        IEnumerable<GraphRow> Rows(){evaluations++;return new[]{new GraphRow(1,first,first),new GraphRow(1,equal,equal)};}
        var view=database.GeneratedView<GraphRow>(Rows,"GraphEquality");
        Assert.Single(view);
        Assert.Equal(1,evaluations);
        object[] argument=[0.0,12m,null!];argument[2]=argument;
        Assert.True(view.Contains(new(1,argument,argument)));
        Assert.Equal(2,evaluations);
        object[] separate=[0.0,12m,null!];separate[2]=separate;
        Assert.False(view.Contains(new(1,argument,separate)));
        Assert.False(view.Contains(new(2,argument,argument)));
        argument[0]=1.0;
        Assert.False(view.Contains(new(1,argument,argument)));
        Assert.Equal(0,database.ResourceUsage.MemoryBytes);
        Assert.Equal(0,database.ResourceUsage.TemporaryStorageBytes);
    }

    [Fact]
    public void ViewContains_DefaultProfileKeepsScalarEqualityAndEmptyResults(){
        using var database=new GraphDatabase(DatabaseContainerOptions.InMemory);
        var view=database.GeneratedView<double>(()=>new[]{-0.0,double.NaN},"ScalarEquality");
        Assert.True(view.Contains(0.0));
        Assert.True(view.Contains(double.NaN));
        Assert.False(view.Contains(1.0));
        var empty=database.GeneratedView<double>(()=>[],"Empty");
        Assert.False(empty.Contains(0.0));
        Assert.Equal(0,database.ResourceUsage.MemoryBytes);
        Assert.Equal(0,database.ResourceUsage.TemporaryStorageBytes);
    }

    [Theory,InlineData(false),InlineData(true)]
    public void RecordOperations_UseNormalizedValuesAndReferenceTopology(bool removeOnly){
        using var database=new GraphDatabase(Options(false));
        object[] stored=[-0.0,12.00m,null!];stored[2]=stored;
        using(var transaction=database.BeginTransaction()){
            Assert.True(database.Items.Add(new(1,stored,stored)));
            transaction.Commit();
        }
        object[] argument=[0.0,12m,null!];argument[2]=argument;
        object[] separate=[0.0,12m,null!];separate[2]=separate;
        using(var transaction=database.BeginTransaction()){
            if(!removeOnly){
                Assert.True(database.Items.Contains(new(1,argument,argument)));
                Assert.False(database.Items.Contains(new(1,argument,separate)));
            }
            Assert.False(database.Items.Remove(new GraphRow(1,argument,separate)));
            Assert.Equal(1,database.Items.Count);
            Assert.True(database.Items.Remove(new GraphRow(1,argument,argument)));
            Assert.Equal(0,database.Items.Count);
            transaction.Commit();
        }
        using(var transaction=database.BeginTransaction()){
            Assert.Equal(0,database.Items.Count);
            transaction.Commit();
        }
    }

    [Theory,InlineData(false),InlineData(true)]
    public async Task CommitAndRollback_IsolateNormalizedGraphs(bool versioned){
        using var database=new GraphDatabase(Options(versioned));
        object[] values=[BitConverter.Int64BitsToDouble(long.MinValue),null!];values[1]=values;
        await using(var transaction=await database.Begin()){
            Assert.True(database.Rows(transaction).Add(new(1,values,values)));
            values[0]=17.0;
            await transaction.CommitAsync();
        }
        var first=await database.Read(1);
        Assert.Same(first.First,first.Second);Assert.Same(first.First,first.First[1]);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits((double)first.First[0]));
        first.First[0]=8.0;
        Assert.Equal(0,(double)(await database.Read(1)).First[0]);
        await using(var transaction=await database.Begin()){
            database.Rows(transaction)[1]=new(1,[99.0],[99.0]);
        }
        Assert.Equal(0,(double)(await database.Read(1)).First[0]);
    }

    [Theory,InlineData(false),InlineData(true)]
    public async Task CheckpointAndReopen_PreserveGraph(bool versioned){
        var root=Path.Combine(Path.GetTempPath(),"Seamless.RecordGraph",Guid.NewGuid().ToString("N"));
        try{
            var options=Options(versioned,root);
            using(var database=new GraphDatabase(options)){
                object[] values=[12.00m,null!];values[1]=values;
                await using var transaction=await database.Begin();database.Rows(transaction).Add(new(1,values,values));
                await transaction.CommitAsync();await database.CheckpointTransactionsAsync();
            }
            using(var database=new GraphDatabase(options)){
                var row=await database.Read(1);
                Assert.Same(row.First,row.Second);Assert.Same(row.First,row.First[1]);
                Assert.Equal(decimal.GetBits(12m),decimal.GetBits((decimal)row.First[0]));
            }
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}
    }

    [Theory,InlineData(false,false),InlineData(false,true),InlineData(true,false),InlineData(true,true)]
    public async Task ScalarCompatibleType_RejectsOtherSavedFormats(bool versioned,bool originalCanonical){
        var root=Path.Combine(Path.GetTempPath(),"Seamless.RecordGraph.Format",Guid.NewGuid().ToString("N"));
        try{
            var options=Options(versioned,root);
            if(originalCanonical){options=options with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1};}
            using(var database=new FlatDatabase(options)){
                await database.Seed();await database.CheckpointTransactionsAsync();
            }
            var other=DatabaseRecordSerialization.CanonicalV1;if(originalCanonical){other=DatabaseRecordSerialization.RecordGraphV1;}
            using var incompatible=new FlatDatabase(options with{RecordSerialization=other});
            await Assert.ThrowsAsync<InvalidDataException>(()=>incompatible.Read());
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}
    }

    [Theory,InlineData(false),InlineData(true)]
    public async Task ChangedRecordType_IsRejectedBeforeLookingUpKeys(bool versioned){
        var root=Path.Combine(Path.GetTempPath(),"Seamless.RecordGraph.Contract",Guid.NewGuid().ToString("N"));
        try{
            var options=Options(versioned,root);
            using(var database=new FlatDatabase(options)){await database.Seed();await database.CheckpointTransactionsAsync();}
            using var changed=new ChangedFlatDatabase(options);
            var error=await Assert.ThrowsAsync<InvalidDataException>(()=>changed.Read());
            Assert.Contains("contract differs",error.Message);
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}
    }

    [Theory,InlineData(false,1048576),InlineData(false,64),InlineData(true,1048576),InlineData(true,64)]
    public async Task GraphKeys_KeepTheSameMeaningInMemoryAndAfterSpill(bool versioned,long memoryLimit){
        using var database=new KeyDatabase(Options(versioned) with{TransactionMemoryLimit=memoryLimit});
        var values=new[]{1.0,-0.0};
        await using(var transaction=await database.Begin()){
            var rows=database.Rows(transaction);Assert.True(rows.Add(new(new(values),1)));values[0]=99;
            var key=new GraphKey([1.0,0.0]);
            Assert.True(rows.ContainsKey(key));Assert.False(rows.Add(new(key,2)));Assert.Equal(1,rows[key].Value);
            await transaction.CommitAsync();
        }
        await using(var read=await database.Begin()){
            Assert.Equal(1,database.Rows(read)[new([1.0,0.0])].Value);await read.CommitAsync();
        }
    }

    [Fact]
    public void SynchronousTransaction_IsolatesSourceAndReturnedGraphs(){
        using var database=new GraphDatabase(Options(false));
        object[] values=[-0.0];
        using(var transaction=database.BeginTransaction()){
            database.Items.Add(new(1,values,values));values[0]=17.0;
            var row=database.Items[1];Assert.Same(row.First,row.Second);Assert.Equal(0,(double)row.First[0]);
            row.First[0]=23.0;Assert.Equal(0,(double)database.Items[1].First[0]);
            object[] summary=[-0.0];database.Load(new(1,summary,summary));summary[0]=19.0;
            var view=database.Summary[1];Assert.Same(view.First,view.Second);Assert.Equal(0,(double)view.First[0]);
            view.First[0]=29.0;Assert.Equal(0,(double)database.Summary[1].First[0]);
            transaction.Commit();
        }
    }

    private static DatabaseContainerOptions Options(bool versioned,string? directory=null){
        var options=DatabaseContainerOptions.InMemory;if(directory is not null){options=DatabaseContainerOptions.OnStorage(directory);}
        options=options with{RecordSerialization=DatabaseRecordSerialization.RecordGraphV1,
            IndexSize=16,PageSize=4096,SegmentSize=1<<16,MemorySize=1<<16,TransactionMemoryLimit=1<<20,ManagedMemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=32<<20};
        if(versioned){options=options with{TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=1<<20,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=1<<20,MaintenanceReserveStorageBytes=4<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}};}
        return options;
    }

    private readonly record struct GraphRow(int Id,object[] First,object[] Second);
    private readonly record struct GraphKey(double[] Values);
    private readonly record struct KeyRow(GraphKey Key,int Value);
    private sealed class KeyDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
        public Table<GraphKey,KeyRow> Items=>this.Table<GraphKey,KeyRow>(static row=>row.Key);
        protected override void EnsureTransactionSchema()=>_ = this.Items;
        public ValueTask<TransactionScope> Begin()=>this.BeginTransactionScopeAsync();
        public TransactionCollection<GraphKey,KeyRow> Rows(TransactionScope scope)=>this.GetTransactionCollection(scope,this.Items);
    }
    private readonly record struct FlatRow(int Id,double Value);
    private sealed class FlatDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
        protected override string TransactionCatalogIdentity=>"record-graph-contract-probe";
        public Table<int,FlatRow> Items=>this.Table<int,FlatRow>(static row=>row.Id);
        protected override void EnsureTransactionSchema()=>_ = this.Items;
        public async Task Seed(){await using var scope=await this.BeginTransactionScopeAsync();this.GetTransactionCollection(scope,this.Items).Add(new(1,-0.0));await scope.CommitAsync();}
        public async Task<FlatRow> Read(){await using var scope=await this.BeginReadOnlyTransactionScopeAsync();return this.GetTransactionCollection(scope,this.Items)[1];}
    }
    private readonly record struct ChangedFlatRow(int Id,long Value);
    private sealed class ChangedFlatDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
        protected override string TransactionCatalogIdentity=>"record-graph-contract-probe";
        public Table<int,ChangedFlatRow> Items=>this.Table<int,ChangedFlatRow>(static row=>row.Id);
        protected override void EnsureTransactionSchema()=>_ = this.Items;
        public async Task<ChangedFlatRow> Read(){await using var scope=await this.BeginReadOnlyTransactionScopeAsync();return this.GetTransactionCollection(scope,this.Items)[1];}
    }
    private sealed class GraphDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
        public Table<int,GraphRow> Items=>this.Table<int,GraphRow>(static row=>row.Id);
        public MaterializedView<int,GraphRow> Summary=>this.MaterializedView<int,GraphRow>(static row=>row.Id);
        public void Load(GraphRow row)=>AddMaterializedViewRow(this.Summary,row);
        protected override void EnsureTransactionSchema(){_ = this.Items;_ = this.Summary;}
        public ValueTask<TransactionScope> Begin()=>this.BeginTransactionScopeAsync();
        public TransactionCollection<int,GraphRow> Rows(TransactionScope scope)=>this.GetTransactionCollection(scope,this.Items);
        public async Task<GraphRow> Read(int key){
            await using var scope=await this.BeginReadOnlyTransactionScopeAsync();var row=this.Rows(scope)[key];await scope.CommitAsync();return row;
        }
    }
}
